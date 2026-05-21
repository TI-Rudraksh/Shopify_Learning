using Microsoft.EntityFrameworkCore;
using ShopifySharp;
using ShopifySharp.Services.Graph;
using ShopifyIntegration.Infrastructure.Data;
using ShopifyIntegration.GraphQL.Queries;
using ShopifyIntegration.GraphQL.Responses.Inventory;

namespace ShopifyIntegration.Jobs;

/// <summary>
/// Compares local inventory quantities against live Shopify values.
/// Logs any discrepancies (drift) and corrects the local DB to match Shopify.
/// This catches cases where inventory webhooks were missed or processed out of order.
/// Runs every 30 minutes. Processes in batches of 50 inventory items per Shopify API call.
/// </summary>
public sealed class InventoryDriftDetectorJob
{
    private const int BatchSize = 50;

    private readonly ShopifyDbContext _db;
    private readonly GraphService     _graphService;
    private readonly ILogger<InventoryDriftDetectorJob> _logger;

    public InventoryDriftDetectorJob(
        ShopifyDbContext db,
        IConfiguration configuration,
        ILogger<InventoryDriftDetectorJob> logger)
    {
        _db           = db;
        _graphService = new GraphService(
            configuration["Shopify:StoreUrl"],
            configuration["Shopify:AccessToken"]);
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var localLevels = await _db.InventoryLevels
            .AsNoTracking()
            .ToListAsync(ct);

        if (localLevels.Count == 0) return;

        _logger.LogInformation(
            "InventoryDriftDetector: checking {Count} inventory level(s) for drift.",
            localLevels.Count);

        var driftCount   = 0;
        var corrected    = 0;

        // Process in batches to stay within Shopify's nodes() limit
        foreach (var batch in localLevels.Chunk(BatchSize))
        {
            var inventoryItemGids = batch
                .Select(l => l.InventoryItemGid)
                .Distinct()
                .ToList();

            var variables = new Dictionary<string, object>
            {
                ["ids"] = inventoryItemGids
            };

            var request  = new GraphRequest { Query = InventoryLevelQueries.GetInventoryLevels, Variables = variables };
            var response = await _graphService.PostAsync<InventoryLevelCheckResponse>(request);
            var nodes    = response.Data?.Nodes;

            if (nodes is null) continue;

            foreach (var itemNode in nodes)
            {
                if (itemNode?.Id is null) continue;

                foreach (var levelEdge in itemNode.InventoryLevels?.Edges ?? [])
                {
                    var levelNode   = levelEdge.Node;
                    var locationGid = levelNode?.Location?.Id;
                    if (locationGid is null) continue;

                    var shopifyQty = levelNode!.Quantities?
                        .FirstOrDefault(q => q.Name == "available")?.Quantity ?? 0;

                    // Find the matching local record
                    var local = batch.FirstOrDefault(l =>
                        l.InventoryItemGid == itemNode.Id &&
                        l.LocationGid      == locationGid);

                    if (local is null) continue;

                    if (local.Quantity != shopifyQty)
                    {
                        driftCount++;
                        _logger.LogWarning(
                            "InventoryDriftDetector: drift detected for InventoryItem={ItemGid} " +
                            "Location={LocationGid}. Local={LocalQty}, Shopify={ShopifyQty}. Correcting.",
                            itemNode.Id, locationGid, local.Quantity, shopifyQty);

                        // Correct the local value to match Shopify (source of truth)
                        var tracked = await _db.InventoryLevels.FindAsync([local.Id], ct);
                        if (tracked is not null)
                        {
                            tracked.Quantity  = shopifyQty;
                            tracked.Available = shopifyQty > 0;
                            tracked.UpdatedAt = DateTimeOffset.UtcNow;
                            corrected++;
                        }
                    }
                }
            }

            await _db.SaveChangesAsync(ct);
        }

        if (driftCount == 0)
        {
            _logger.LogInformation("InventoryDriftDetector: no drift found. Local DB is in sync.");
        }
        else
        {
            _logger.LogWarning(
                "InventoryDriftDetector: found {Drift} drift(s), corrected {Corrected}.",
                driftCount, corrected);
        }
    }
}
