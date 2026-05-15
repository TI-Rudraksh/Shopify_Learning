using MediatR;
using ShopifyIntegration.Domain.Entities;
using ShopifyIntegration.Domain.Repositories;
using ShopifyIntegration.Infrastructure.Data.Helpers;

namespace ShopifyIntegration.Features.Webhooks.Commands;

public sealed class HandleProductUpdatedCommandHandler
    : IRequestHandler<HandleProductUpdatedCommand, Unit>
{
    private readonly IProductRepository _products;
    private readonly IWebhookEventRepository _webhookEvents;

    public HandleProductUpdatedCommandHandler(
        IProductRepository products,
        IWebhookEventRepository webhookEvents)
    {
        _products      = products;
        _webhookEvents = webhookEvents;
    }

    public async Task<Unit> Handle(
        HandleProductUpdatedCommand command, CancellationToken cancellationToken)
    {
        if (await _webhookEvents.ExistsProcessedAsync(
                "products/update", command.NumericId, cancellationToken))
            return Unit.Value;

        try
        {
            var product = new Product
            {
                ShopifyGid = ShopifyGidHelper.BuildProductGid(command.NumericId),
                NumericId  = command.NumericId,
                Title      = command.Title,
                Vendor     = command.Vendor,
                Status     = command.Status,
                CreatedAt  = DateTimeOffset.UtcNow,
                UpdatedAt  = command.UpdatedAt.ToUniversalTime(),
            };
            await _products.UpsertAsync(product, cancellationToken);

            await _webhookEvents.AddAsync(new WebhookEvent
            {
                Topic            = "products/update",
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
                Topic            = "products/update",
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
