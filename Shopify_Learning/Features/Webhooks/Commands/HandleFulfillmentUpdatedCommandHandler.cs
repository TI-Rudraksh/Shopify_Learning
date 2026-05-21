using MediatR;
using ShopifyIntegration.Domain.Entities;
using ShopifyIntegration.Domain.Repositories;
using ShopifyIntegration.Infrastructure.Data.Helpers;

namespace ShopifyIntegration.Features.Webhooks.Commands;

/// <summary>
/// Handles the fulfillments/update Shopify webhook.
///
/// Shopify fires this topic when a fulfillment's status changes after creation
/// — e.g. pending → success, tracking number added, fulfillment cancelled.
///
/// The payload has the same shape as fulfillments/create (FulfillmentWebhook).
///
/// What we update:
///   - Fulfillment.Status
///   - Fulfillment.TrackingNumber / TrackingCompany / TrackingUrl
///   - Fulfillment.UpdatedAt
///   - Order.FulfillmentStatus — recalculated from all fulfillments for the order
///
/// Idempotency: we upsert by ShopifyGid, so replaying the same webhook is safe.
/// </summary>
public sealed class HandleFulfillmentUpdatedCommandHandler
    : IRequestHandler<HandleFulfillmentUpdatedCommand, Unit>
{
    private readonly IFulfillmentRepository  _fulfillments;
    private readonly IOrderRepository        _orders;
    private readonly IWebhookEventRepository _webhookEvents;
    private readonly ILogger<HandleFulfillmentUpdatedCommandHandler> _logger;

    public HandleFulfillmentUpdatedCommandHandler(
        IFulfillmentRepository  fulfillments,
        IOrderRepository        orders,
        IWebhookEventRepository webhookEvents,
        ILogger<HandleFulfillmentUpdatedCommandHandler> logger)
    {
        _fulfillments  = fulfillments;
        _orders        = orders;
        _webhookEvents = webhookEvents;
        _logger        = logger;
    }

    public async Task<Unit> Handle(
        HandleFulfillmentUpdatedCommand command, CancellationToken cancellationToken)
    {
        var payload = command.Payload;

        try
        {
            var fulfillmentGid = ShopifyGidHelper.BuildFulfillmentGid(payload.Id);

            // Upsert the fulfillment row — FulfillmentRepository.UpsertAsync
            // matches on ShopifyGid and updates status + tracking fields.
            var fulfillment = new Fulfillment
            {
                ShopifyGid      = fulfillmentGid,
                NumericId       = payload.Id,
                Status          = payload.Status,
                TrackingNumber  = payload.TrackingNumber,
                TrackingCompany = payload.TrackingCompany,
                TrackingUrl     = payload.TrackingUrl,
                UpdatedAt       = payload.UpdatedAt.ToUniversalTime(),
                // OrderId and CreatedAt are preserved by UpsertAsync on existing rows
                OrderId         = 0,   // placeholder — overwritten if row exists
                CreatedAt       = payload.CreatedAt.ToUniversalTime(),
            };

            // Resolve the local order so we can set the FK and update order status
            var order = await _orders.GetByNumericIdAsync(payload.OrderId, cancellationToken);
            if (order is not null)
                fulfillment.OrderId = order.Id;

            await _fulfillments.UpsertAsync(fulfillment, cancellationToken);

            // Recalculate order fulfillment status from all fulfillments
            if (order is not null)
            {
                var allFulfillments = await _fulfillments.GetAllForOrderAsync(
                    order.Id, cancellationToken);

                var hasSuccess   = allFulfillments.Any(f => f.Status == "success");
                var allSuccess   = allFulfillments.All(f => f.Status == "success");
                var hasCancelled = allFulfillments.All(f => f.Status is "cancelled" or "error");

                order.FulfillmentStatus = hasCancelled ? "unfulfilled"
                    : allSuccess         ? "fulfilled"
                    : hasSuccess         ? "partial"
                    :                      order.FulfillmentStatus; // no change if nothing succeeded yet

                order.UpdatedAt = DateTimeOffset.UtcNow;
                await _orders.UpsertAsync(order, cancellationToken);
            }

            _logger.LogInformation(
                "Fulfillment updated via webhook: FulfillmentGid={Gid}, Status={Status}.",
                fulfillmentGid, payload.Status);

            await _webhookEvents.AddAsync(new WebhookEvent
            {
                Topic            = "fulfillments/update",
                ShopifyNumericId = payload.Id,
                RawPayload       = "",
                ProcessedAt      = DateTimeOffset.UtcNow,
                Status           = "processed",
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            await _webhookEvents.AddAsync(new WebhookEvent
            {
                Topic            = "fulfillments/update",
                ShopifyNumericId = payload.Id,
                RawPayload       = "",
                ProcessedAt      = DateTimeOffset.UtcNow,
                Status           = "failed",
                ErrorMessage     = ex.Message,
            }, cancellationToken);
            throw;
        }

        return Unit.Value;
    }
}
