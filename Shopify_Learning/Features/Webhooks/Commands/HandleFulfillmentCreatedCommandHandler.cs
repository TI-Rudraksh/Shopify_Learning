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

    public HandleFulfillmentCreatedCommandHandler(
        IFulfillmentRepository  fulfillments,
        IOrderRepository        orders,
        IWebhookEventRepository webhookEvents)
    {
        _fulfillments  = fulfillments;
        _orders        = orders;
        _webhookEvents = webhookEvents;
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
            // Resolve the local order FK
            var order = await _orders.GetByNumericIdAsync(payload.OrderId, cancellationToken);

            if (order is not null)
            {
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
