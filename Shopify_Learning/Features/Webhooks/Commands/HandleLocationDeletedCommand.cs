using MediatR;

namespace ShopifyIntegration.Features.Webhooks.Commands;

public sealed record HandleLocationDeletedCommand(long NumericId) : IRequest<Unit>;
