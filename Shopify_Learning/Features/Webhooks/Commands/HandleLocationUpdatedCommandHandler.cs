using MediatR;
using ShopifyIntegration.Domain.Entities;
using ShopifyIntegration.Domain.Repositories;
using ShopifyIntegration.Infrastructure.Data.Helpers;

namespace ShopifyIntegration.Features.Webhooks.Commands;

public sealed class HandleLocationUpdatedCommandHandler
    : IRequestHandler<HandleLocationUpdatedCommand, Unit>
{
    private readonly ILocationRepository _locations;

    public HandleLocationUpdatedCommandHandler(ILocationRepository locations)
        => _locations = locations;

    public async Task<Unit> Handle(
        HandleLocationUpdatedCommand command, CancellationToken cancellationToken)
    {
        var location = new Location
        {
            LocationGid = ShopifyGidHelper.BuildLocationGid(command.NumericId),
            NumericId   = command.NumericId,
            Name        = command.Name,
            CreatedAt   = DateTimeOffset.UtcNow,
            UpdatedAt   = command.UpdatedAt.ToUniversalTime(),
        };

        await _locations.UpsertAsync(location, cancellationToken);
        return Unit.Value;
    }
}
