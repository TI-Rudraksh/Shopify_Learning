using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShopifyIntegration.DTOs;
using ShopifyIntegration.Features.Inventory.Commands;
using ShopifyIntegration.Infrastructure.Data.Helpers;

namespace ShopifyIntegration.API.Controllers;

[ApiController]
[Route("api/stores/{storeId}/products/{productId}/inventory")]
public sealed class InventoryController : ControllerBase
{
    private readonly IMediator _mediator;

    public InventoryController(IMediator mediator) => _mediator = mediator;

    [HttpPatch]
    public async Task<IActionResult> UpdateInventory(
        long storeId,
        long productId,
        [FromBody] UpdateInventoryRequest request,
        CancellationToken ct)
    {
        var command = new UpdateInventoryCommand(
            ProductGid:  ShopifyGidHelper.BuildProductGid(productId),
            LocationGid: ShopifyGidHelper.BuildLocationGid(storeId),
            Quantity:    request.Quantity,
            Available:   request.Available);

        var result = await _mediator.Send(command, ct);
        return result is null ? NotFound() : Accepted(result);
    }
}
