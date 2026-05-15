using MediatR;

namespace ShopifyIntegration.Features.Webhooks.Commands;

public sealed record ProcessShopifyWebhookCommand(byte[] RawBody, string Topic)
    : IRequest<Unit>;
