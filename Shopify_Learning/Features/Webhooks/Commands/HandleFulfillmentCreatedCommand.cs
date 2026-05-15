using MediatR;
using ShopifyIntegration.Features.Webhooks.Models;

namespace ShopifyIntegration.Features.Webhooks.Commands;

public sealed record HandleFulfillmentCreatedCommand(FulfillmentWebhook Payload) : IRequest<Unit>;
