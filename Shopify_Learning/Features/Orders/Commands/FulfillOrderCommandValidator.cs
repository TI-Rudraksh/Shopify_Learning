using FluentValidation;

namespace ShopifyIntegration.Features.Orders.Commands;

public sealed class FulfillOrderCommandValidator : AbstractValidator<FulfillOrderCommand>
{
    public FulfillOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}
