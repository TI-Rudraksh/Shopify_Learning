using ShopifyIntegration.Domain.Entities;

namespace ShopifyIntegration.Domain.Repositories;

public interface IOrderRepository
{
    /// <summary>
    /// Tries to find an order by local int Id, then NumericId, then ShopifyGid.
    /// </summary>
    Task<Order?> GetByAnyIdAsync(string orderId, CancellationToken ct = default);
    Task<Order?> GetByShopifyGidAsync(string shopifyGid, CancellationToken ct = default);
    Task<Order?> GetByNumericIdAsync(long numericId, CancellationToken ct = default);
    Task<Order>  UpsertAsync(Order order, CancellationToken ct = default);
    Task<List<Order>> GetAllAsync(CancellationToken ct = default);
}
