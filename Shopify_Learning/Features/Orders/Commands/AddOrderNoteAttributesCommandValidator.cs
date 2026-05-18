using FluentValidation;

namespace ShopifyIntegration.Features.Orders.Commands;

public sealed class AddOrderNoteAttributesCommandValidator
    : AbstractValidator<AddOrderNoteAttributesCommand>
{
    public AddOrderNoteAttributesCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();

        // At least one of note or attributes must be provided
        RuleFor(x => x)
            .Must(x => x.Note is not null || x.Attributes.Count > 0)
            .WithMessage("At least one of 'note' or 'note_attributes' must be provided.");

        RuleFor(x => x.Attributes).ForEach(rule =>
        {
            rule.Must(a => !string.IsNullOrWhiteSpace(a.Name))
                .WithMessage("Each note attribute must have a non-empty name.");
        });
    }
}
