using MediatR;
using ShopifyIntegration.Domain.Repositories;

namespace ShopifyIntegration.Features.Webhooks.Commands;

public sealed class HandleLocationDeletedCommandHandler
    : IRequestHandler<HandleLocationDeletedCommand, Unit>
{
    private readonly ILocationRepository _locations;

    public HandleLocationDeletedCommandHandler(ILocationRepository locations)
        => _locations = locations;

    public async Task<Unit> Handle(
        HandleLocationDeletedCommand command, CancellationToken cancellationToken)
    {
        await _locations.DeleteByNumericIdAsync(command.NumericId, cancellationToken);
        return Unit.Value;
    }
}
