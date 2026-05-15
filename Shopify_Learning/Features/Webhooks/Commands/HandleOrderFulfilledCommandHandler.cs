using MediatR;
using ShopifyIntegration.Domain.Repositories;
using ShopifyIntegration.Domain.Entities;
using ShopifyIntegration.Infrastructure.Data.Helpers;

namespace ShopifyIntegration.Features.Webhooks.Commands;

public sealed class HandleOrderFulfilledCommandHandler
    : IRequestHandler<HandleOrderFulfilledCommand, Unit>
{
    private readonly IOrderRepository        _orders;
    private readonly IWebhookEventRepository _webhookEvents;

    public HandleOrderFulfilledCommandHandler(
        IOrderRepository        orders,
        IWebhookEventRepository webhookEvents)
    {
        _orders        = orders;
        _webhookEvents = webhookEvents;
    }

    public async Task<Unit> Handle(
        HandleOrderFulfilledCommand command, CancellationToken cancellationToken)
    {
        var payload = command.Payload;

        try
        {
            var order = await _orders.GetByNumericIdAsync(payload.Id, cancellationToken);
            if (order is not null)
            {
                order.FulfillmentStatus = "fulfilled";
                order.UpdatedAt         = payload.UpdatedAt.ToUniversalTime();
                await _orders.UpsertAsync(order, cancellationToken);
            }

            await _webhookEvents.AddAsync(new WebhookEvent
            {
                Topic            = "orders/fulfilled",
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
                Topic            = "orders/fulfilled",
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
