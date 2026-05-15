using MediatR;

namespace ShopifyIntegration.Features.Webhooks.Commands;

public sealed record HandleProductUpdatedCommand(
    long NumericId, string Title, string Vendor, string Status, DateTimeOffset UpdatedAt)
    : IRequest<Unit>;
