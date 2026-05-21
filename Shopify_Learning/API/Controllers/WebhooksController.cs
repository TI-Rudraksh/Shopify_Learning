using Hangfire;
using Microsoft.AspNetCore.Mvc;
using ShopifyIntegration.Infrastructure.Shopify.Validators;
using ShopifyIntegration.Jobs;

namespace ShopifyIntegration.API.Controllers;

[ApiController]
[Route("api/webhooks")]
public sealed class WebhooksController : ControllerBase
{
    private readonly IBackgroundJobClient _jobs;
    private readonly IShopifyWebhookValidator _validator;

    public WebhooksController(IBackgroundJobClient jobs, IShopifyWebhookValidator validator)
    {
        _jobs      = jobs;
        _validator = validator;
    }

    /// <summary>
    /// Receives a Shopify webhook, validates the HMAC signature, then immediately
    /// enqueues a ProcessWebhookJob and returns 200 OK to Shopify (under 5 ms).
    ///
    /// Processing happens asynchronously in the background. Hangfire retries
    /// automatically on failure (3 attempts: 60 s → 300 s → 900 s), replacing
    /// the need for the manual WebhookReprocessorJob for newly-ingested events.
    ///
    /// This eliminates the risk of Shopify timing out and re-delivering the same
    /// webhook, which previously caused duplicate-processing pressure on the
    /// idempotency checks.
    /// </summary>
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

        // Enqueue and return immediately — Shopify gets 200 in < 5 ms
        _jobs.Enqueue<ProcessWebhookJob>(
            job => job.ExecuteAsync(rawBody, topicValues.ToString(), CancellationToken.None));

        return Ok();
    }
}
