using MediatR;
using ShopifyIntegration.Domain.Entities;
using ShopifyIntegration.Domain.Repositories;

namespace ShopifyIntegration.Features.Webhooks.Commands;

public sealed class HandleProductDeletedCommandHandler
    : IRequestHandler<HandleProductDeletedCommand, Unit>
{
    private readonly IProductRepository _products;
    private readonly IWebhookEventRepository _webhookEvents;

    public HandleProductDeletedCommandHandler(
        IProductRepository products,
        IWebhookEventRepository webhookEvents)
    {
        _products      = products;
        _webhookEvents = webhookEvents;
    }

    public async Task<Unit> Handle(
        HandleProductDeletedCommand command, CancellationToken cancellationToken)
    {
        if (await _webhookEvents.ExistsProcessedAsync(
                "products/delete", command.NumericId, cancellationToken))
            return Unit.Value;

        try
        {
            await _products.DeleteByNumericIdAsync(command.NumericId, cancellationToken);

            await _webhookEvents.AddAsync(new WebhookEvent
            {
                Topic            = "products/delete",
                ShopifyNumericId = command.NumericId,
                RawPayload       = "",
                ProcessedAt      = DateTimeOffset.UtcNow,
                Status           = "processed",
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            await _webhookEvents.AddAsync(new WebhookEvent
            {
                Topic            = "products/delete",
                ShopifyNumericId = command.NumericId,
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
