using Microsoft.EntityFrameworkCore;
using ShopifyIntegration.Domain.Entities;
using ShopifyIntegration.Domain.Repositories;

namespace ShopifyIntegration.Infrastructure.Data.Repositories;

public sealed class ProductRepository : IProductRepository
{
    private readonly ShopifyDbContext _db;
    private readonly ILogger<ProductRepository> _logger;

    public ProductRepository(ShopifyDbContext db, ILogger<ProductRepository> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<Product> UpsertAsync(Product product, CancellationToken ct = default)
    {
        var existing = await _db.Products
            .FirstOrDefaultAsync(p => p.ShopifyGid == product.ShopifyGid, ct);

        if (existing is null)
        {
            _db.Products.Add(product);
        }
        else
        {
            existing.Title     = product.Title;
            existing.Vendor    = product.Vendor;
            existing.Status    = product.Status;
            existing.UpdatedAt = product.UpdatedAt;
        }

        await _db.SaveChangesAsync(ct);
        return existing ?? product;
    }

    public Task<Product?> GetByShopifyGidAsync(string shopifyGid, CancellationToken ct = default)
        => _db.Products.FirstOrDefaultAsync(p => p.ShopifyGid == shopifyGid, ct);

    public Task<Product?> GetByNumericIdAsync(long numericId, CancellationToken ct = default)
        => _db.Products.FirstOrDefaultAsync(p => p.NumericId == numericId, ct);

    public async Task<bool> DeleteByNumericIdAsync(long numericId, CancellationToken ct = default)
    {
        var rows = await _db.Products
            .Where(p => p.NumericId == numericId)
            .ExecuteDeleteAsync(ct);
        return rows > 0;
    }

    public Task<List<Product>> GetAllAsync(CancellationToken ct = default)
        => _db.Products.ToListAsync(ct);
}
