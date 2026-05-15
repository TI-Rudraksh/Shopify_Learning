using ShopifyIntegration.Domain.Entities;

namespace ShopifyIntegration.Domain.Repositories;

public interface ICustomerRepository
{
    Task<Customer>  UpsertAsync(Customer customer, CancellationToken ct = default);
    Task<Customer?> GetByShopifyGidAsync(string shopifyGid, CancellationToken ct = default);
    Task<Customer?> GetByNumericIdAsync(long numericId, CancellationToken ct = default);
}
