using FluentValidation;

namespace ShopifyIntegration.Features.Products.Commands;

public sealed class DeleteProductCommandValidator : AbstractValidator<DeleteProductCommand>
{
    public DeleteProductCommandValidator()
    {
        RuleFor(x => x.ShopifyGid).NotEmpty();
    }
}
