using MediatR;

namespace ShopifyIntegration.Features.Webhooks.Commands;

public sealed record HandleProductDeletedCommand(long NumericId) : IRequest<Unit>;
