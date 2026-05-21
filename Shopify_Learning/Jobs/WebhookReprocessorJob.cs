using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopifyIntegration.Features.Webhooks.Commands;
using ShopifyIntegration.Infrastructure.Data;

namespace ShopifyIntegration.Jobs;

/// <summary>
/// Finds webhook events that failed processing and retries them.
/// Runs every 5 minutes. Gives up after 3 total attempts (tracked via ErrorMessage prefix).
/// This solves the real problem of transient Shopify API errors or DB timeouts
/// causing webhooks to be permanently lost.
/// </summary>
public sealed class WebhookReprocessorJob
{
    private const int MaxAttempts = 3;

    private readonly ShopifyDbContext _db;
    private readonly IMediator        _mediator;
    private readonly ILogger<WebhookReprocessorJob> _logger;

    public WebhookReprocessorJob(
        ShopifyDbContext db,
        IMediator mediator,
        ILogger<WebhookReprocessorJob> logger)
    {
        _db       = db;
        _mediator = mediator;
        _logger   = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        // Find failed events that still have a raw payload and haven't exceeded max attempts
        var failedEvents = await _db.WebhookEvents
            .Where(e => e.Status == "failed"
                     && e.RawPayload != ""
                     && !e.ErrorMessage!.StartsWith($"[DEAD]"))
            .OrderBy(e => e.ProcessedAt)
            .Take(50)
            .ToListAsync(ct);

        if (failedEvents.Count == 0) return;

        _logger.LogInformation(
            "WebhookReprocessor: found {Count} failed webhook(s) to retry.", failedEvents.Count);

        foreach (var evt in failedEvents)
        {
            // Count previous attempts from the error message prefix "[ATTEMPT n]"
            var attempts = CountAttempts(evt.ErrorMessage);

            if (attempts >= MaxAttempts)
            {
                evt.Status       = "failed";
                evt.ErrorMessage = $"[DEAD] Exceeded {MaxAttempts} retry attempts. Last error: {evt.ErrorMessage}";
                _logger.LogWarning(
                    "WebhookReprocessor: marking webhook {Id} (topic={Topic}) as dead-letter after {Max} attempts.",
                    evt.Id, evt.Topic, MaxAttempts);
                continue;
            }

            try
            {
                var rawBytes = System.Text.Encoding.UTF8.GetBytes(evt.RawPayload);
                await _mediator.Send(new ProcessShopifyWebhookCommand(rawBytes, evt.Topic), ct);

                evt.Status       = "processed";
                evt.ErrorMessage = null;
                evt.ProcessedAt  = DateTimeOffset.UtcNow;

                _logger.LogInformation(
                    "WebhookReprocessor: successfully reprocessed webhook {Id} (topic={Topic}).",
                    evt.Id, evt.Topic);
            }
            catch (Exception ex)
            {
                evt.ErrorMessage = $"[ATTEMPT {attempts + 1}] {ex.Message}";
                evt.ProcessedAt  = DateTimeOffset.UtcNow;

                _logger.LogWarning(ex,
                    "WebhookReprocessor: retry attempt {Attempt}/{Max} failed for webhook {Id} (topic={Topic}).",
                    attempts + 1, MaxAttempts, evt.Id, evt.Topic);
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private static int CountAttempts(string? errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage)) return 0;
        // Scan for the highest [ATTEMPT n] prefix
        for (var i = MaxAttempts; i >= 1; i--)
            if (errorMessage.Contains($"[ATTEMPT {i}]")) return i;
        return 0;
    }
}
