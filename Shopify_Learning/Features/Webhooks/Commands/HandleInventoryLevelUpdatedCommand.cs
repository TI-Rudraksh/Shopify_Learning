using MediatR;
using ShopifyIntegration.Features.Webhooks.Models;

namespace ShopifyIntegration.Features.Webhooks.Commands;

/// <summary>
/// Dispatched when Shopify fires an inventory_levels/update webhook.
/// Carries the raw webhook payload.
/// </summary>
public sealed record HandleInventoryLevelUpdatedCommand(InventoryLevelWebhook Payload)
    : IRequest<Unit>;
