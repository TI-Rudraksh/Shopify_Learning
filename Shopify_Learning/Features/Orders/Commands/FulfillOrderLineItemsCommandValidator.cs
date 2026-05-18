using FluentValidation;

namespace ShopifyIntegration.Features.Orders.Commands;

public sealed class FulfillOrderLineItemsCommandValidator : AbstractValidator<FulfillOrderLineItemsCommand>
{
    public FulfillOrderLineItemsCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.LineItemIds).NotNull().NotEmpty();
        RuleFor(x => x.LineItemIds).ForEach(rule => rule.Must(id => !string.IsNullOrWhiteSpace(id)));
    }
}
