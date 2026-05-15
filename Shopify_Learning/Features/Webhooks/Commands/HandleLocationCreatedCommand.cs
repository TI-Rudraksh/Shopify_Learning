using MediatR;

namespace ShopifyIntegration.Features.Webhooks.Commands;

public sealed record HandleLocationCreatedCommand(
    long NumericId, string Name, DateTimeOffset UpdatedAt)
    : IRequest<Unit>;
