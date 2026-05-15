using Microsoft.EntityFrameworkCore;
using ShopifyIntegration.Domain.Entities;
using ShopifyIntegration.Domain.Repositories;

namespace ShopifyIntegration.Infrastructure.Data.Repositories;

public sealed class LocationRepository : ILocationRepository
{
    private readonly ShopifyDbContext _db;
    private readonly ILogger<LocationRepository> _logger;

    public LocationRepository(ShopifyDbContext db, ILogger<LocationRepository> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<Location> UpsertAsync(Location location, CancellationToken ct = default)
    {
        var existing = await _db.Locations
            .FirstOrDefaultAsync(l => l.LocationGid == location.LocationGid, ct);

        if (existing is null)
        {
            location.CreatedAt = DateTimeOffset.UtcNow;
            location.UpdatedAt = DateTimeOffset.UtcNow;
            _db.Locations.Add(location);
        }
        else
        {
            existing.NumericId = location.NumericId;
            existing.Name      = location.Name;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return existing ?? location;
    }

    public Task<Location?> GetByGidAsync(string locationGid, CancellationToken ct = default)
        => _db.Locations.FirstOrDefaultAsync(l => l.LocationGid == locationGid, ct);

    public async Task<bool> DeleteByNumericIdAsync(long numericId, CancellationToken ct = default)
    {
        var rows = await _db.Locations
            .Where(l => l.NumericId == numericId)
            .ExecuteDeleteAsync(ct);
        return rows > 0;
    }
}
