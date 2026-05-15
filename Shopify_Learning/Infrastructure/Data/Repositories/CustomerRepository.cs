using Microsoft.EntityFrameworkCore;
using ShopifyIntegration.Domain.Entities;
using ShopifyIntegration.Domain.Repositories;

namespace ShopifyIntegration.Infrastructure.Data.Repositories;

public sealed class CustomerRepository : ICustomerRepository
{
    private readonly ShopifyDbContext _db;
    private readonly ILogger<CustomerRepository> _logger;

    public CustomerRepository(ShopifyDbContext db, ILogger<CustomerRepository> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<Customer> UpsertAsync(Customer customer, CancellationToken ct = default)
    {
        var existing = await _db.Customers
            .FirstOrDefaultAsync(c => c.ShopifyGid == customer.ShopifyGid, ct);

        if (existing is null)
        {
            _db.Customers.Add(customer);
        }
        else
        {
            existing.Email            = customer.Email;
            existing.FirstName        = customer.FirstName;
            existing.LastName         = customer.LastName;
            existing.Phone            = customer.Phone;
            existing.AcceptsMarketing = customer.AcceptsMarketing;
            existing.UpdatedAt        = customer.UpdatedAt;
        }

        await _db.SaveChangesAsync(ct);
        return existing ?? customer;
    }

    public Task<Customer?> GetByShopifyGidAsync(string shopifyGid, CancellationToken ct = default)
        => _db.Customers.FirstOrDefaultAsync(c => c.ShopifyGid == shopifyGid, ct);

    public Task<Customer?> GetByNumericIdAsync(long numericId, CancellationToken ct = default)
        => _db.Customers.FirstOrDefaultAsync(c => c.NumericId == numericId, ct);
}
