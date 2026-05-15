using MediatR;

namespace ShopifyIntegration.Features.Inventory.Commands;

public sealed record UpdateInventoryCommand(
    string ProductGid,
    string LocationGid,
    int    Quantity,
    bool   Available = true)
    : IRequest<UpdateInventoryResult?>;

public sealed record UpdateInventoryResult(
    string         ProductGid,
    string         LocationGid,
    string         InventoryItemGid,
    int            Quantity,
    bool           Available,
    DateTimeOffset UpdatedAt);
