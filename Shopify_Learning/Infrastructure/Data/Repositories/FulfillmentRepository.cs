using Microsoft.EntityFrameworkCore;
using ShopifyIntegration.Domain.Entities;
using ShopifyIntegration.Domain.Repositories;

namespace ShopifyIntegration.Infrastructure.Data.Repositories;

public sealed class FulfillmentRepository : IFulfillmentRepository
{
    private readonly ShopifyDbContext _db;
    private readonly ILogger<FulfillmentRepository> _logger;

    public FulfillmentRepository(ShopifyDbContext db, ILogger<FulfillmentRepository> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<Fulfillment> UpsertAsync(Fulfillment fulfillment, CancellationToken ct = default)
    {
        var existing = await _db.Fulfillments
            .FirstOrDefaultAsync(f => f.ShopifyGid == fulfillment.ShopifyGid, ct);

        if (existing is null)
        {
            _db.Fulfillments.Add(fulfillment);
        }
        else
        {
            existing.Status              = fulfillment.Status;
            existing.TrackingNumber      = fulfillment.TrackingNumber;
            existing.TrackingCompany     = fulfillment.TrackingCompany;
            existing.TrackingUrl         = fulfillment.TrackingUrl;
            existing.FulfillmentOrderGid = fulfillment.FulfillmentOrderGid;
            existing.UpdatedAt           = fulfillment.UpdatedAt;

            // Only overwrite OrderId if the caller resolved a real one (> 0).
            // The fulfillments/update webhook handler passes 0 as a placeholder
            // when the order isn't found locally — we must not corrupt the FK.
            if (fulfillment.OrderId > 0)
                existing.OrderId = fulfillment.OrderId;

            // Preserve FulfilledLineItemGids set by the outbound fulfillment path.
            // Webhook-driven updates don't carry line item GIDs, so we never
            // overwrite a non-null value with null here.
            if (!string.IsNullOrWhiteSpace(fulfillment.FulfilledLineItemGids))
                existing.FulfilledLineItemGids = fulfillment.FulfilledLineItemGids;
        }

        await _db.SaveChangesAsync(ct);
        return existing ?? fulfillment;
    }

    public Task<Fulfillment?> GetByShopifyGidAsync(string shopifyGid, CancellationToken ct = default)
        => _db.Fulfillments.FirstOrDefaultAsync(f => f.ShopifyGid == shopifyGid, ct);

    public Task<List<Fulfillment>> GetAllForOrderAsync(int orderId, CancellationToken ct = default)
        => _db.Fulfillments
            .Where(f => f.OrderId == orderId)
            .ToListAsync(ct);
}
