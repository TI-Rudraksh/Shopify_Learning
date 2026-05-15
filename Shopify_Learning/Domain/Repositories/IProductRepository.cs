using ShopifyIntegration.Domain.Entities;

namespace ShopifyIntegration.Domain.Repositories;

public interface IProductRepository
{
    Task<Product>       UpsertAsync(Product product, CancellationToken ct = default);
    Task<Product?>      GetByShopifyGidAsync(string shopifyGid, CancellationToken ct = default);
    Task<Product?>      GetByNumericIdAsync(long numericId, CancellationToken ct = default);
    Task<bool>          DeleteByNumericIdAsync(long numericId, CancellationToken ct = default);
    Task<List<Product>> GetAllAsync(CancellationToken ct = default);
}
