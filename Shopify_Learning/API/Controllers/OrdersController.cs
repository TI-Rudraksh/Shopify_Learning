using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShopifyIntegration.DTOs;
using ShopifyIntegration.Features.Orders.Commands;
using ShopifyIntegration.Features.Orders.Queries;

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

    /// <summary>
    /// Returns the note and note attributes for the given order.
    /// orderId can be a local int Id, a Shopify numeric Id, or a Shopify GID.
    /// </summary>
    [HttpGet("{orderId}/note")]
    public async Task<IActionResult> GetOrderNote(string orderId, CancellationToken ct = default)
    {
        var query  = new GetOrderNoteQuery(orderId);
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Updates the note text and/or appends note attributes for the given order.
    /// Accepts the Shopify-native shape: { "note": "...", "note_attributes": [{name, value}] }.
    /// At least one of note or note_attributes must be provided.
    /// Existing attributes are preserved; incoming ones with the same name overwrite.
    /// Also syncs changes to Shopify via the orderUpdate mutation.
    /// orderId can be a local int Id, a Shopify numeric Id, or a Shopify GID.
    /// </summary>
    [HttpPost("{orderId}/note")]
    public async Task<IActionResult> UpdateOrderNote(
        string orderId,
        [FromBody] UpdateOrderNoteRequest request,
        CancellationToken ct = default)
    {
        var inputs  = request.NoteAttributes
            .Select(a => new NoteAttributeInput(a.Name, a.Value))
            .ToList()
            .AsReadOnly();
        var command = new AddOrderNoteAttributesCommand(orderId, request.Note, inputs);
        var result  = await _mediator.Send(command, ct);
        return Accepted(result);
    }
}
