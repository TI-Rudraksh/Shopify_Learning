using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShopifyIntegration.DTOs;
using ShopifyIntegration.Features.Orders.Commands;

namespace ShopifyIntegration.API.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;

    public OrdersController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Fulfills all open fulfillment orders for the given order.
    /// orderId can be a local int Id, a Shopify numeric Id, or a Shopify GID.
    /// </summary>
    [HttpPost("{orderId}/fulfill")]
    public async Task<IActionResult> FulfillOrder(
        string orderId,
        [FromQuery] string? trackingNumber  = null,
        [FromQuery] string? trackingCompany = null,
        CancellationToken ct = default)
    {
        var command = new FulfillOrderCommand(orderId, trackingNumber, trackingCompany);
        var result  = await _mediator.Send(command, ct);
        return Accepted(result);
    }

    /// <summary>
    /// Fulfills specific line items of the given order.
    /// orderId can be a local int Id, a Shopify numeric Id, or a Shopify GID.
    /// </summary>
    [HttpPost("{orderId}/fulfill-items")]
    public async Task<IActionResult> FulfillOrderLineItems(
        string orderId,
        [FromBody] PartialFulfillmentRequest request,
        CancellationToken ct = default)
    {
        var command = new FulfillOrderLineItemsCommand(
            orderId,
            request.LineItemIds,
            request.TrackingNumber,
            request.TrackingCompany,
            request.NotifyCustomer);
        var result = await _mediator.Send(command, ct);
        return Accepted(result);
    }
}
