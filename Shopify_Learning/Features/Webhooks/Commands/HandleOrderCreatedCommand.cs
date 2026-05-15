using MediatR;
using ShopifyIntegration.Features.Webhooks.Models;

namespace ShopifyIntegration.Features.Webhooks.Commands;

public sealed record HandleOrderCreatedCommand(OrderWebhook Payload) : IRequest<Unit>;
