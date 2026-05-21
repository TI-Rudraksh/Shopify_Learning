using Microsoft.EntityFrameworkCore;
using ShopifyIntegration.Domain.Entities;
using ShopifyIntegration.Domain.Repositories;

namespace ShopifyIntegration.Infrastructure.Data.Repositories;

public sealed class InventoryRepository : IInventoryRepository
{
    private readonly ShopifyDbContext _db;
    private readonly ILogger<InventoryRepository> _logger;

    public InventoryRepository(ShopifyDbContext db, ILogger<InventoryRepository> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<InventoryLevel> UpsertAsync(InventoryLevel level, CancellationToken ct = default)
    {
        // Natural key is (inventory_item_gid, location_gid) — one row per
        // inventory item per location. A product with multiple variants has
        // multiple inventory items, so keying on product_id alone is wrong.
        var existing = await _db.InventoryLevels
            .FirstOrDefaultAsync(
                il => il.InventoryItemGid == level.InventoryItemGid
                   && il.LocationGid      == level.LocationGid,
                ct);

        if (existing is null)
        {
            level.CreatedAt = DateTimeOffset.UtcNow;
            level.UpdatedAt = DateTimeOffset.UtcNow;
            _db.InventoryLevels.Add(level);
        }
        else
        {
            existing.ProductId        = level.ProductId;
            existing.InventoryItemGid = level.InventoryItemGid;
            existing.Quantity         = level.Quantity;
            existing.Available        = level.Available;
            existing.UpdatedAt        = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return existing ?? level;
    }

    public Task<InventoryLevel?> GetByProductAndLocationAsync(int productId, string locationGid, CancellationToken ct = default)
        => _db.InventoryLevels
            .FirstOrDefaultAsync(il => il.ProductId == productId && il.LocationGid == locationGid, ct);

    public Task<List<InventoryLevel>> GetAllForProductAsync(int productId, CancellationToken ct = default)
        => _db.InventoryLevels
            .Where(il => il.ProductId == productId)
            .ToListAsync(ct);
}
