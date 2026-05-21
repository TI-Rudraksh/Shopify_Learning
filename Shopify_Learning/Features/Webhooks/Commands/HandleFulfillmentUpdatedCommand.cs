using MediatR;
using ShopifyIntegration.Features.Webhooks.Models;

namespace ShopifyIntegration.Features.Webhooks.Commands;

/// <summary>
/// Dispatched when Shopify fires a fulfillments/update webhook.
/// Carries the raw webhook payload (same shape as fulfillments/create).
/// </summary>
public sealed record HandleFulfillmentUpdatedCommand(FulfillmentWebhook Payload)
    : IRequest<Unit>;
