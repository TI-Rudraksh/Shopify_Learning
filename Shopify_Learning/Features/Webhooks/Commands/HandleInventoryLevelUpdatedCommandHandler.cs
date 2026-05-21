using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopifyIntegration.Domain.Entities;
using ShopifyIntegration.Domain.Repositories;
using ShopifyIntegration.Infrastructure.Data;
using ShopifyIntegration.Infrastructure.Data.Helpers;

namespace ShopifyIntegration.Features.Webhooks.Commands;

/// <summary>
/// Handles the inventory_levels/update Shopify webhook.
///
/// This is the real-time path for keeping inventory_levels in sync.
/// Shopify fires this topic whenever available quantity changes at a location
/// — sales, manual adjustments, returns, transfers, etc.
///
/// The webhook payload contains:
///   inventory_item_id — numeric ID → we build the GID
///   location_id       — numeric ID → we build the GID
///   available         — current available quantity (null = not tracked)
///
/// We look up the local InventoryLevel row by (inventory_item_gid, location_gid)
/// and update the quantity. If no row exists yet (e.g. new product/location),
/// we try to find the parent product and insert a new row.
///
/// No idempotency check needed — quantity updates are idempotent by nature.
/// </summary>
public sealed class HandleInventoryLevelUpdatedCommandHandler
    : IRequestHandler<HandleInventoryLevelUpdatedCommand, Unit>
{
    private readonly ShopifyDbContext        _db;
    private readonly IWebhookEventRepository _webhookEvents;
    private readonly ILogger<HandleInventoryLevelUpdatedCommandHandler> _logger;

    public HandleInventoryLevelUpdatedCommandHandler(
        ShopifyDbContext        db,
        IWebhookEventRepository webhookEvents,
        ILogger<HandleInventoryLevelUpdatedCommandHandler> logger)
    {
        _db            = db;
        _webhookEvents = webhookEvents;
        _logger        = logger;
    }

    public async Task<Unit> Handle(
        HandleInventoryLevelUpdatedCommand command, CancellationToken cancellationToken)
    {
        var payload = command.Payload;

        try
        {
            var inventoryItemGid = ShopifyGidHelper.BuildInventoryItemGid(payload.InventoryItemId);
            var locationGid      = ShopifyGidHelper.BuildLocationGid(payload.LocationId);
            var newQty           = payload.Available ?? 0;

            // Try to find an existing row by the natural key (inventory_item_gid, location_gid)
            var existing = await _db.InventoryLevels
                .FirstOrDefaultAsync(
                    il => il.InventoryItemGid == inventoryItemGid
                       && il.LocationGid      == locationGid,
                    cancellationToken);

            if (existing is not null)
            {
                // Fast path: row exists — just update quantity
                existing.Quantity  = newQty;
                existing.Available = newQty > 0;
                existing.UpdatedAt = payload.UpdatedAt.ToUniversalTime();

                await _db.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "InventoryLevel updated via webhook: InventoryItem={ItemGid}, Location={LocationGid}, Available={Qty}.",
                    inventoryItemGid, locationGid, newQty);
            }
            else
            {
                // Row doesn't exist yet — try to find the parent product so we can set the FK.
                // We match by InventoryItemGid on any existing InventoryLevel row for this item,
                // or fall back to finding a product that has this inventory item via the DB.
                var productId = await ResolveProductIdAsync(inventoryItemGid, cancellationToken);

                if (productId is null)
                {
                    // Product not tracked locally yet — log and skip.
                    // FullInventorySyncJob will pick this up on Sunday.
                    _logger.LogWarning(
                        "InventoryLevel webhook: no local product found for InventoryItem={ItemGid}. " +
                        "Row will be created by FullInventorySyncJob.",
                        inventoryItemGid);
                }
                else
                {
                    _db.InventoryLevels.Add(new InventoryLevel
                    {
                        ProductId        = productId.Value,
                        InventoryItemGid = inventoryItemGid,
                        LocationGid      = locationGid,
                        Quantity         = newQty,
                        Available        = newQty > 0,
                        CreatedAt        = payload.UpdatedAt.ToUniversalTime(),
                        UpdatedAt        = payload.UpdatedAt.ToUniversalTime(),
                    });

                    await _db.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation(
                        "InventoryLevel inserted via webhook: InventoryItem={ItemGid}, Location={LocationGid}, Available={Qty}.",
                        inventoryItemGid, locationGid, newQty);
                }
            }

            await _webhookEvents.AddAsync(new WebhookEvent
            {
                Topic       = "inventory_levels/update",
                RawPayload  = "",
                ProcessedAt = DateTimeOffset.UtcNow,
                Status      = "processed",
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            await _webhookEvents.AddAsync(new WebhookEvent
            {
                Topic        = "inventory_levels/update",
                RawPayload   = "",
                ProcessedAt  = DateTimeOffset.UtcNow,
                Status       = "failed",
                ErrorMessage = ex.Message,
            }, cancellationToken);
            throw;
        }

        return Unit.Value;
    }

    /// <summary>
    /// Tries to resolve the local Product.Id for a given InventoryItemGid.
    /// Looks for any existing InventoryLevel row that already has this GID
    /// (fastest path), then falls back to scanning products via their
    /// InventoryLevel rows.
    /// </summary>
    private async Task<int?> ResolveProductIdAsync(
        string inventoryItemGid, CancellationToken ct)
    {
        // If any other InventoryLevel row already has this inventory item GID,
        // reuse its ProductId (same product, different location).
        var sibling = await _db.InventoryLevels
            .AsNoTracking()
            .Where(il => il.InventoryItemGid == inventoryItemGid)
            .Select(il => (int?)il.ProductId)
            .FirstOrDefaultAsync(ct);

        return sibling;
    }
}
