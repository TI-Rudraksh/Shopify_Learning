using FluentValidation;

namespace ShopifyIntegration.Features.Products.Commands;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty();
        RuleFor(x => x.Vendor).NotEmpty();
    }
}
