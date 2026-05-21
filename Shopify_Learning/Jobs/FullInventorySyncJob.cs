using Hangfire;
using Microsoft.EntityFrameworkCore;
using ShopifySharp;
using ShopifySharp.Services.Graph;
using ShopifyIntegration.Domain.Entities;
using ShopifyIntegration.GraphQL.Queries;
using ShopifyIntegration.GraphQL.Responses.Inventory;
using ShopifyIntegration.Infrastructure.Data;
using ShopifyIntegration.Infrastructure.Data.Helpers;
using InventoryLevelEntity = ShopifyIntegration.Domain.Entities.InventoryLevel;

namespace ShopifyIntegration.Jobs;

/// <summary>
/// Weekly full reconciliation: paginates through every product in Shopify,
/// fetches all inventory levels across all locations, and upserts them locally.
///
/// This is the safety net for the InventoryDriftDetectorJob — it handles the
/// case where a product or location was added in Shopify while the app was
/// down, so the item never made it into the local DB at all.
///
/// Runs Sunday at 01:00 UTC (before WebhookCleanupJob at 02:00).
/// Processes 25 products per GraphQL page to stay within Shopify's cost limits.
///
/// Summary logged at the end:
///   FullInventorySync: processed 120 product(s) — added 5, updated 38, unchanged 77.
/// </summary>
public sealed class FullInventorySyncJob
{
    private const int PageSize = 25;

    private readonly ShopifyDbContext _db;
    private readonly GraphService     _graphService;
    private readonly ILogger<FullInventorySyncJob> _logger;

    public FullInventorySyncJob(
        ShopifyDbContext db,
        IConfiguration configuration,
        ILogger<FullInventorySyncJob> logger)
    {
        _db           = db;
        _graphService = new GraphService(
            configuration["Shopify:StoreUrl"],
            configuration["Shopify:AccessToken"]);
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [300, 900])]
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("FullInventorySync: starting full reconciliation.");

        string? cursor       = null;
        var     totalPages   = 0;
        var     totalAdded   = 0;
        var     totalUpdated = 0;

        // Pre-load all local products into a GID → local Id lookup to avoid
        // N+1 queries inside the loop.
        var productGidToId = await _db.Products
            .AsNoTracking()
            .ToDictionaryAsync(p => p.ShopifyGid, p => p.Id, ct);

        do
        {
            var variables = new Dictionary<string, object> { ["first"] = PageSize };
            if (cursor is not null) variables["after"] = cursor;

            var request  = new GraphRequest
            {
                Query     = FullInventorySyncQueries.GetAllProductInventory,
                Variables = variables,
            };
            var response = await _graphService.PostAsync<FullInventorySyncResponse>(request);
            var products = response.Data?.Products;

            if (products?.Edges is null) break;

            totalPages++;

            foreach (var productEdge in products.Edges)
            {
                var productNode = productEdge.Node;
                if (productNode?.Id is null) continue;

                // Only sync inventory for products we already track locally.
                // New products arrive via webhooks / OrderSyncJob; this job
                // corrects quantities, not product existence.
                if (!productGidToId.TryGetValue(productNode.Id, out var localProductId))
                    continue;

                foreach (var variantEdge in productNode.Variants?.Edges ?? [])
                {
                    var inventoryItem = variantEdge.Node?.InventoryItem;
                    if (inventoryItem?.Id is null) continue;

                    foreach (var levelEdge in inventoryItem.InventoryLevels?.Edges ?? [])
                    {
                        var levelNode   = levelEdge.Node;
                        var locationGid = levelNode?.Location?.Id;
                        if (locationGid is null) continue;

                        var shopifyQty = levelNode!.Quantities?
                            .FirstOrDefault(q => q.Name == "available")?.Quantity ?? 0;

                        var (added, updated) = await UpsertInventoryLevelAsync(
                            localProductId,
                            inventoryItem.Id,
                            locationGid,
                            shopifyQty,
                            ct);

                        totalAdded   += added   ? 1 : 0;
                        totalUpdated += updated ? 1 : 0;
                    }
                }
            }

            cursor = products.PageInfo?.HasNextPage == true
                ? products.PageInfo.EndCursor
                : null;

        } while (cursor is not null);

        _logger.LogInformation(
            "FullInventorySync: completed {Pages} page(s) — added {Added}, updated {Updated}.",
            totalPages, totalAdded, totalUpdated);
    }

    /// <summary>
    /// Upserts a single inventory level row.
    /// Returns (added: bool, updated: bool).
    /// </summary>
    private async Task<(bool added, bool updated)> UpsertInventoryLevelAsync(
        int    localProductId,
        string inventoryItemGid,
        string locationGid,
        int    shopifyQty,
        CancellationToken ct)
    {
        var existing = await _db.InventoryLevels
            .FirstOrDefaultAsync(
                il => il.ProductId        == localProductId
                   && il.InventoryItemGid == inventoryItemGid
                   && il.LocationGid      == locationGid,
                ct);

        if (existing is null)
        {
            _db.InventoryLevels.Add(new InventoryLevelEntity
            {
                ProductId        = localProductId,
                InventoryItemGid = inventoryItemGid,
                LocationGid      = locationGid,
                Quantity         = shopifyQty,
                Available        = shopifyQty > 0,
                CreatedAt        = DateTimeOffset.UtcNow,
                UpdatedAt        = DateTimeOffset.UtcNow,
            });
            await _db.SaveChangesAsync(ct);
            return (added: true, updated: false);
        }

        if (existing.Quantity == shopifyQty)
            return (added: false, updated: false);

        existing.Quantity  = shopifyQty;
        existing.Available = shopifyQty > 0;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return (added: false, updated: true);
    }
}
