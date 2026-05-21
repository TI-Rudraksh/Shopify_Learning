using MediatR;
using ShopifyIntegration.Domain.Entities;
using ShopifyIntegration.Domain.Repositories;
using ShopifyIntegration.Infrastructure.Data.Helpers;

namespace ShopifyIntegration.Features.Webhooks.Commands;

public sealed class HandleFulfillmentCreatedCommandHandler
    : IRequestHandler<HandleFulfillmentCreatedCommand, Unit>
{
    private readonly IFulfillmentRepository  _fulfillments;
    private readonly IOrderRepository        _orders;
    private readonly IWebhookEventRepository _webhookEvents;
    private readonly ILogger<HandleFulfillmentCreatedCommandHandler> _logger;

    public HandleFulfillmentCreatedCommandHandler(
        IFulfillmentRepository  fulfillments,
        IOrderRepository        orders,
        IWebhookEventRepository webhookEvents,
        ILogger<HandleFulfillmentCreatedCommandHandler> logger)
    {
        _fulfillments  = fulfillments;
        _orders        = orders;
        _webhookEvents = webhookEvents;
        _logger        = logger;
    }

    public async Task<Unit> Handle(
        HandleFulfillmentCreatedCommand command, CancellationToken cancellationToken)
    {
        var payload = command.Payload;

        if (await _webhookEvents.ExistsProcessedAsync(
                "fulfillments/create", payload.Id, cancellationToken))
            return Unit.Value;

        try
        {
            var order = await _orders.GetByNumericIdAsync(payload.OrderId, cancellationToken);

            if (order is not null)
            {
                // Upsert the fulfillment row
                var fulfillment = new Fulfillment
                {
                    ShopifyGid      = ShopifyGidHelper.BuildFulfillmentGid(payload.Id),
                    NumericId       = payload.Id,
                    OrderId         = order.Id,
                    Status          = payload.Status,
                    TrackingNumber  = payload.TrackingNumber,
                    TrackingCompany = payload.TrackingCompany,
                    TrackingUrl     = payload.TrackingUrl,
                    CreatedAt       = payload.CreatedAt.ToUniversalTime(),
                    UpdatedAt       = payload.UpdatedAt.ToUniversalTime(),
                };

                await _fulfillments.UpsertAsync(fulfillment, cancellationToken);

                // Update order fulfillment status based on all fulfillments for this order.
                // A single "success" fulfillment means at least partial; we set "fulfilled"
                // only when the orders/fulfilled webhook confirms it, or when all line items
                // are covered. Here we conservatively set "partial" on first fulfillment
                // and let orders/fulfilled upgrade it to "fulfilled".
                if (payload.Status == "success" &&
                    order.FulfillmentStatus is "unfulfilled" or null or "")
                {
                    order.FulfillmentStatus = "partial";
                    order.UpdatedAt         = DateTimeOffset.UtcNow;
                    await _orders.UpsertAsync(order, cancellationToken);

                    _logger.LogInformation(
                        "Order {OrderId} fulfillment status set to 'partial' via fulfillments/create webhook.",
                        payload.OrderId);
                }
            }
            else
            {
                _logger.LogWarning(
                    "fulfillments/create webhook: order {OrderId} not found locally. " +
                    "Fulfillment not persisted — will be picked up by OrderSyncJob.",
                    payload.OrderId);
            }

            await _webhookEvents.AddAsync(new WebhookEvent
            {
                Topic            = "fulfillments/create",
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
                Topic            = "fulfillments/create",
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
