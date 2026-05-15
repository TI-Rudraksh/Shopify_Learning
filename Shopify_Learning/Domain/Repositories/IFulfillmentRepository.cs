using ShopifyIntegration.Domain.Entities;

namespace ShopifyIntegration.Domain.Repositories;

public interface IFulfillmentRepository
{
    Task<Fulfillment>       UpsertAsync(Fulfillment fulfillment, CancellationToken ct = default);
    Task<Fulfillment?>      GetByShopifyGidAsync(string shopifyGid, CancellationToken ct = default);
    Task<List<Fulfillment>> GetAllForOrderAsync(int orderId, CancellationToken ct = default);
}
