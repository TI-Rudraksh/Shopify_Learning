using FluentValidation;

namespace ShopifyIntegration.Features.Webhooks.Commands;

public sealed class ProcessShopifyWebhookCommandValidator
    : AbstractValidator<ProcessShopifyWebhookCommand>
{
    public ProcessShopifyWebhookCommandValidator()
    {
        RuleFor(x => x.Topic).NotEmpty();
        RuleFor(x => x.RawBody).NotEmpty();
    }
}
