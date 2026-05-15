using MediatR;

namespace ShopifyIntegration.Features.Webhooks.Commands;

public sealed record HandleProductCreatedCommand(
    long NumericId, string Title, string Vendor, string Status, DateTimeOffset UpdatedAt)
    : IRequest<Unit>;
