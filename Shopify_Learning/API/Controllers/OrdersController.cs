using Hangfire;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShopifyIntegration.DTOs;
using ShopifyIntegration.Features.Orders.Commands;
using ShopifyIntegration.Features.Orders.Queries;
using ShopifyIntegration.Jobs;

namespace ShopifyIntegration.API.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IBackgroundJobClient _jobs;

    public OrdersController(IMediator mediator, IBackgroundJobClient jobs)
    {
        _mediator = mediator;
        _jobs     = jobs;
    }

    /// <summary>
    /// Enqueues a background job to fulfill all open fulfillment orders for the given order.
    /// Returns 202 Accepted immediately with the Hangfire job ID — the actual Shopify API
    /// call happens asynchronously, so the client is never blocked by Shopify latency or
    /// rate limits.
    /// orderId can be a local int Id, a Shopify numeric Id, or a Shopify GID.
    /// </summary>
    [HttpPost("{orderId}/fulfill")]
    public IActionResult FulfillOrder(
        string orderId,
        [FromQuery] string? trackingNumber  = null,
        [FromQuery] string? trackingCompany = null,
        [FromQuery] bool    notifyCustomer  = true)
    {
        var jobId = _jobs.Enqueue<FulfillOrderJob>(
            job => job.ExecuteAsync(orderId, trackingNumber, trackingCompany, notifyCustomer,
                CancellationToken.None));

        // Chain a notification job that fires only if fulfillment succeeds
        _jobs.ContinueJobWith<SendFulfillmentNotificationJob>(
            jobId,
            job => job.ExecuteAsync(orderId, CancellationToken.None));

        return Accepted(new { jobId, message = "Fulfillment enqueued." });
    }

    /// <summary>
    /// Enqueues a background job to fulfill specific line items of the given order.
    /// Returns 202 Accepted immediately with the Hangfire job ID.
    /// orderId can be a local int Id, a Shopify numeric Id, or a Shopify GID.
    /// </summary>
    [HttpPost("{orderId}/fulfill-items")]
    public IActionResult FulfillOrderLineItems(
        string orderId,
        [FromBody] PartialFulfillmentRequest request)
    {
        var jobId = _jobs.Enqueue<FulfillOrderLineItemsJob>(
            job => job.ExecuteAsync(
                orderId,
                request.LineItemIds,
                request.TrackingNumber,
                request.TrackingCompany,
                request.NotifyCustomer,
                CancellationToken.None));

        // Chain a notification job that fires only if fulfillment succeeds
        _jobs.ContinueJobWith<SendFulfillmentNotificationJob>(
            jobId,
            job => job.ExecuteAsync(orderId, CancellationToken.None));

        return Accepted(new { jobId, message = "Partial fulfillment enqueued." });
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
