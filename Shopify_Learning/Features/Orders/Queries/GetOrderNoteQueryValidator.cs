using FluentValidation;

namespace ShopifyIntegration.Features.Orders.Queries;

public sealed class GetOrderNoteQueryValidator : AbstractValidator<GetOrderNoteQuery>
{
    public GetOrderNoteQueryValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}
