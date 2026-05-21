using Microsoft.EntityFrameworkCore;
using ShopifySharp;
using ShopifySharp.Services.Graph;
using ShopifyIntegration.Infrastructure.Data;
using ShopifyIntegration.GraphQL.Queries;
using ShopifyIntegration.GraphQL.Responses.Fulfillment;

namespace ShopifyIntegration.Jobs;

/// <summary>
/// Finds fulfillments that are locally recorded as "pending" or "in_progress"
/// but haven't been updated in over 2 hours, then checks their real status in Shopify.
/// If Shopify shows them as "success" or "cancelled", the local record is corrected
/// and the parent order's fulfillment status is updated accordingly.
/// Runs daily. Catches cases where the fulfillments/create webhook was missed.
/// </summary>
public sealed class StaleFulfillmentCheckerJob
{
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromHours(2);

    private readonly ShopifyDbContext _db;
    private readonly GraphService     _graphService;
    private readonly ILogger<StaleFulfillmentCheckerJob> _logger;

    public StaleFulfillmentCheckerJob(
        ShopifyDbContext db,
        IConfiguration configuration,
        ILogger<StaleFulfillmentCheckerJob> logger)
    {
        _db           = db;
        _graphService = new GraphService(
            configuration["Shopify:StoreUrl"],
            configuration["Shopify:AccessToken"]);
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var staleThresholdTime = DateTimeOffset.UtcNow - StaleThreshold;

        var staleFulfillments = await _db.Fulfillments
            .Include(f => f.Order)
            .Where(f => (f.Status == "pending" || f.Status == "in_progress")
                     && f.UpdatedAt < staleThresholdTime)
            .ToListAsync(ct);

        if (staleFulfillments.Count == 0)
        {
            _logger.LogInformation("StaleFulfillmentChecker: no stale fulfillments found.");
            return;
        }

        _logger.LogInformation(
            "StaleFulfillmentChecker: checking {Count} stale fulfillment(s).",
            staleFulfillments.Count);

        var corrected = 0;

        foreach (var fulfillment in staleFulfillments)
        {
            try
            {
                var variables = new Dictionary<string, object> { ["id"] = fulfillment.ShopifyGid };
                var request   = new GraphRequest { Query = FulfillmentStatusQueries.GetFulfillmentStatus, Variables = variables };
                var response  = await _graphService.PostAsync<FulfillmentStatusResponse>(request);
                var node      = response.Data?.Fulfillment;

                if (node?.Status is null) continue;

                var shopifyStatus = node.Status.ToLowerInvariant();

                if (shopifyStatus == fulfillment.Status) continue;

                _logger.LogWarning(
                    "StaleFulfillmentChecker: fulfillment {Gid} is '{LocalStatus}' locally " +
                    "but '{ShopifyStatus}' in Shopify. Correcting.",
                    fulfillment.ShopifyGid, fulfillment.Status, shopifyStatus);

                fulfillment.Status    = shopifyStatus;
                fulfillment.UpdatedAt = node.UpdatedAt.ToUniversalTime();

                // If the fulfillment is now terminal, update the parent order status
                if (shopifyStatus == "success" && fulfillment.Order is not null)
                {
                    var order = fulfillment.Order;

                    // Check if all line items are now covered
                    var allFulfillments = await _db.Fulfillments
                        .Where(f => f.OrderId == order.Id && f.Status == "success")
                        .ToListAsync(ct);

                    // Simple heuristic: if any fulfillment is success, at minimum partial
                    order.FulfillmentStatus = allFulfillments.Count > 0 ? "fulfilled" : "partial";
                    order.UpdatedAt         = DateTimeOffset.UtcNow;
                }

                corrected++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "StaleFulfillmentChecker: failed to check fulfillment {Gid}.",
                    fulfillment.ShopifyGid);
            }
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "StaleFulfillmentChecker: corrected {Count} fulfillment(s).", corrected);
    }
}
