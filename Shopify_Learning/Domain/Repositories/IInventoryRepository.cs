using ShopifyIntegration.Domain.Entities;

namespace ShopifyIntegration.Domain.Repositories;

public interface IInventoryRepository
{
    Task<InventoryLevel>       UpsertAsync(InventoryLevel level, CancellationToken ct = default);
    Task<InventoryLevel?>      GetByProductAndLocationAsync(int productId, string locationGid, CancellationToken ct = default);
    Task<List<InventoryLevel>> GetAllForProductAsync(int productId, CancellationToken ct = default);
}
