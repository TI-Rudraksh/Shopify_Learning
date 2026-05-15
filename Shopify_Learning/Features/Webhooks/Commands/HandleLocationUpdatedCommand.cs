using MediatR;

namespace ShopifyIntegration.Features.Webhooks.Commands;

public sealed record HandleLocationUpdatedCommand(
    long NumericId, string Name, DateTimeOffset UpdatedAt)
    : IRequest<Unit>;
