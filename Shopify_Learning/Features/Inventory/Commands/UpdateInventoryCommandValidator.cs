using FluentValidation;

namespace ShopifyIntegration.Features.Inventory.Commands;

public sealed class UpdateInventoryCommandValidator : AbstractValidator<UpdateInventoryCommand>
{
    public UpdateInventoryCommandValidator()
    {
        RuleFor(x => x.ProductGid).NotEmpty();
        RuleFor(x => x.LocationGid).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0);
    }
}
