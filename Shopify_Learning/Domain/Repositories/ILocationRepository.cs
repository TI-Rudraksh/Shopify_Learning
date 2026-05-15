using ShopifyIntegration.Domain.Entities;

namespace ShopifyIntegration.Domain.Repositories;

public interface ILocationRepository
{
    Task<Location>  UpsertAsync(Location location, CancellationToken ct = default);
    Task<Location?> GetByGidAsync(string locationGid, CancellationToken ct = default);
    Task<bool>      DeleteByNumericIdAsync(long numericId, CancellationToken ct = default);
}
