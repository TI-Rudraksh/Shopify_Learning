using Hangfire;
using MediatR;
using ShopifyIntegration.Features.Webhooks.Commands;

namespace ShopifyIntegration.Jobs;

/// <summary>
/// Processes a Shopify webhook payload in the background.
///
/// The webhook controller enqueues this job and returns 200 OK to Shopify
/// immediately (under 5 ms), eliminating the risk of Shopify timing out and
/// retrying the delivery.
///
/// Hangfire's built-in retry replaces the manual WebhookReprocessorJob retry
/// loop for newly-ingested webhooks. The WebhookReprocessorJob is kept for
/// events that were recorded as "failed" before this job existed (backwards
/// compatibility) and for any edge-case where the job itself is lost.
///
/// Retry schedule (seconds): 60 → 300 → 900  (3 attempts total)
/// After all attempts are exhausted Hangfire marks the job as Failed and it
/// appears in the dashboard for manual inspection.
/// </summary>
public sealed class ProcessWebhookJob
{
    private readonly IMediator _mediator;
    private readonly ILogger<ProcessWebhookJob> _logger;

    public ProcessWebhookJob(IMediator mediator, ILogger<ProcessWebhookJob> logger)
    {
        _mediator = mediator;
        _logger   = logger;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [60, 300, 900],
        OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task ExecuteAsync(byte[] rawBody, string topic, CancellationToken ct)
    {
        _logger.LogInformation("ProcessWebhookJob: processing topic={Topic}.", topic);

        await _mediator.Send(new ProcessShopifyWebhookCommand(rawBody, topic), ct);

        _logger.LogInformation("ProcessWebhookJob: completed topic={Topic}.", topic);
    }
}
