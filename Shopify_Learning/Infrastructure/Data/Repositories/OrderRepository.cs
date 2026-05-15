using Microsoft.EntityFrameworkCore;
using ShopifyIntegration.Domain.Entities;
using ShopifyIntegration.Domain.Repositories;

namespace ShopifyIntegration.Infrastructure.Data.Repositories;

public sealed class OrderRepository : IOrderRepository
{
    private readonly ShopifyDbContext _db;
    private readonly ILogger<OrderRepository> _logger;

    public OrderRepository(ShopifyDbContext db, ILogger<OrderRepository> logger)
    {
        _db     = db;
        _logger = logger;
    }

    /// <summary>
    /// Tries local int Id first, then NumericId, then ShopifyGid.
    /// </summary>
    public async Task<Order?> GetByAnyIdAsync(string orderId, CancellationToken ct = default)
    {
        // Try local int Id
        if (int.TryParse(orderId, out var localId))
        {
            var byLocalId = await _db.Orders
                .Include(o => o.LineItems)
                .Include(o => o.Fulfillments)
                .AsSplitQuery()
                .FirstOrDefaultAsync(o => o.Id == localId, ct);
            if (byLocalId is not null) return byLocalId;
        }

        // Try NumericId
        if (long.TryParse(orderId, out var numericId))
        {
            var byNumericId = await _db.Orders
                .Include(o => o.LineItems)
                .Include(o => o.Fulfillments)
                .AsSplitQuery()
                .FirstOrDefaultAsync(o => o.NumericId == numericId, ct);
            if (byNumericId is not null) return byNumericId;
        }

        // Try ShopifyGid
        return await _db.Orders
            .Include(o => o.LineItems)
            .Include(o => o.Fulfillments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.ShopifyGid == orderId, ct);
    }

    public Task<Order?> GetByShopifyGidAsync(string shopifyGid, CancellationToken ct = default)
        => _db.Orders
            .Include(o => o.LineItems)
            .Include(o => o.Fulfillments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.ShopifyGid == shopifyGid, ct);

    public Task<Order?> GetByNumericIdAsync(long numericId, CancellationToken ct = default)
        => _db.Orders
            .Include(o => o.LineItems)
            .Include(o => o.Fulfillments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.NumericId == numericId, ct);

    public async Task<Order> UpsertAsync(Order order, CancellationToken ct = default)
    {
        var existing = await _db.Orders
            .Include(o => o.LineItems)
            .FirstOrDefaultAsync(o => o.ShopifyGid == order.ShopifyGid, ct);

        if (existing is null)
        {
            _db.Orders.Add(order);
        }
        else
        {
            existing.Name              = order.Name;
            existing.FinancialStatus   = order.FinancialStatus;
            existing.FulfillmentStatus = order.FulfillmentStatus;
            existing.TotalPrice        = order.TotalPrice;
            existing.Currency          = order.Currency;
            existing.CustomerId        = order.CustomerId;
            existing.CancelledAt       = order.CancelledAt;
            existing.UpdatedAt         = order.UpdatedAt;

            // Replace line items
            _db.OrderLineItems.RemoveRange(existing.LineItems);
            existing.LineItems = order.LineItems;
        }

        await _db.SaveChangesAsync(ct);
        return existing ?? order;
    }

    public Task<List<Order>> GetAllAsync(CancellationToken ct = default)
        => _db.Orders
            .Include(o => o.LineItems)
            .Include(o => o.Fulfillments)
            .AsSplitQuery()
            .ToListAsync(ct);
}
