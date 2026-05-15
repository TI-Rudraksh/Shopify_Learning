using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShopifyIntegration.Features.Webhooks.Commands;
using ShopifyIntegration.Infrastructure.Shopify.Validators;

namespace ShopifyIntegration.API.Controllers;

[ApiController]
[Route("api/webhooks")]
public sealed class WebhooksController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IShopifyWebhookValidator _validator;

    public WebhooksController(IMediator mediator, IShopifyWebhookValidator validator)
    {
        _mediator  = mediator;
        _validator = validator;
    }

    [HttpPost("shopify")]
    public async Task<IActionResult> ReceiveShopifyWebhook(CancellationToken ct)
    {
        if (!Request.Headers.TryGetValue("X-Shopify-Hmac-SHA256", out var hmacValues)
            || string.IsNullOrEmpty(hmacValues))
            return BadRequest();

        if (!Request.Headers.TryGetValue("X-Shopify-Topic", out var topicValues)
            || string.IsNullOrEmpty(topicValues))
            return BadRequest();

        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms, ct);
        var rawBody = ms.ToArray();

        var validation = _validator.Validate(rawBody, hmacValues.ToString());
        if (!validation.IsValid)
            return Unauthorized();

        await _mediator.Send(
            new ProcessShopifyWebhookCommand(rawBody, topicValues.ToString()), ct);
        return Ok();
    }
}
