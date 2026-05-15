using FluentValidation;

namespace ShopifyIntegration.Features.Products.Commands;

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.ShopifyGid).NotEmpty();
        RuleFor(x => x.Title).NotEmpty();
    }
}
