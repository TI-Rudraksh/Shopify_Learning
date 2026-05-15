using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShopifyIntegration.Domain.Entities;
using ShopifyIntegration.Domain.Repositories;
using ShopifyIntegration.Infrastructure.Data.Helpers;
using ShopifyIntegration.Infrastructure.Shopify;

namespace ShopifyIntegration.Features.Inventory.Commands;

public sealed class UpdateInventoryCommandHandler
    : IRequestHandler<UpdateInventoryCommand, UpdateInventoryResult?>
{
    private readonly IProductRepository        _products;
    private readonly IShopifyInventoryService  _shopifyInventory;
    private readonly IInventoryRepository      _inventory;
    private readonly ILocationRepository       _locations;
    private readonly ILogger<UpdateInventoryCommandHandler> _logger;

    public UpdateInventoryCommandHandler(
        IProductRepository        products,
        IShopifyInventoryService  shopifyInventory,
        IInventoryRepository      inventory,
        ILocationRepository       locations,
        ILogger<UpdateInventoryCommandHandler> logger)
    {
        _products         = products;
        _shopifyInventory = shopifyInventory;
        _inventory        = inventory;
        _locations        = locations;
        _logger           = logger;
    }

    public async Task<UpdateInventoryResult?> Handle(
        UpdateInventoryCommand command, CancellationToken cancellationToken)
    {
        // Step 1: Resolve the InventoryItemGid from Shopify.
        // We do not require the product to exist in the local DB — the GID is
        // sufficient to call Shopify. If the product GID is invalid Shopify will
        // return an error which propagates as ShopifyInventoryException.
        var inventoryItemGid = await _shopifyInventory.GetInventoryItemGidAsync(
            command.ProductGid, cancellationToken);

        // Step 2: Ensure the inventory item is activated at the location (idempotent),
        // then set the on-hand quantity.
        await _shopifyInventory.ActivateInventoryItemAsync(
            inventoryItemGid, command.LocationGid, cancellationToken);

        await _shopifyInventory.SetOnHandQuantityAsync(
            inventoryItemGid, command.LocationGid, command.Quantity, cancellationToken);

        // Step 3: Upsert InventoryLevel in local DB (best-effort; keyed on ProductGid + LocationGid).
        // Look up the local product to get its PK for the FK — if it doesn't exist yet we
        // skip the local persistence step rather than blocking the Shopify update.
        var now = DateTimeOffset.UtcNow;
        var product = await _products.GetByShopifyGidAsync(command.ProductGid, cancellationToken);
        if (product is not null)
        {
            var inventoryLevel = new InventoryLevel
            {
                ProductId        = product.Id,
                LocationGid      = command.LocationGid,
                InventoryItemGid = inventoryItemGid,
                Quantity         = command.Quantity,
                Available        = command.Available,
                CreatedAt        = now,
                UpdatedAt        = now,
            };

            try
            {
                await _inventory.UpsertAsync(inventoryLevel, cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Retry once before propagating
                await _inventory.UpsertAsync(inventoryLevel, cancellationToken);
            }
        }
        else
        {
            _logger.LogWarning(
                "Product {ProductGid} not found in local DB — inventory updated in Shopify but not persisted locally.",
                command.ProductGid);
        }

        // Step 4: Upsert Location
        var numericId = ShopifyGidHelper.ParseNumericId(command.LocationGid);
        var location = new Location
        {
            LocationGid = command.LocationGid,
            NumericId   = numericId,
            Name        = $"Location {numericId}",
            CreatedAt   = now,
            UpdatedAt   = now,
        };
        await _locations.UpsertAsync(location, cancellationToken);

        // Step 5: Log success
        _logger.LogInformation(
            "Inventory updated successfully. InventoryItemGid={InventoryItemGid}, LocationGid={LocationGid}, Quantity={Quantity}",
            inventoryItemGid, command.LocationGid, command.Quantity);

        // Step 6: Return result
        return new UpdateInventoryResult(
            ProductGid:       command.ProductGid,
            LocationGid:      command.LocationGid,
            InventoryItemGid: inventoryItemGid,
            Quantity:         command.Quantity,
            Available:        command.Available,
            UpdatedAt:        now);
    }
}
