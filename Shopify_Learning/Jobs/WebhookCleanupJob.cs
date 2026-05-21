using Microsoft.EntityFrameworkCore;
using ShopifyIntegration.Infrastructure.Data;

namespace ShopifyIntegration.Jobs;

/// <summary>
/// Purges old webhook_events rows to keep the table from growing unbounded.
/// Retention policy:
///   - "processed" and "skipped" events older than 30 days → deleted
///   - "failed" (dead-letter) events older than 90 days → deleted
/// Runs weekly (Sunday at 02:00 UTC).
/// </summary>
public sealed class WebhookCleanupJob
{
    private static readonly TimeSpan ProcessedRetention  = TimeSpan.FromDays(30);
    private static readonly TimeSpan DeadLetterRetention = TimeSpan.FromDays(90);

    private readonly ShopifyDbContext _db;
    private readonly ILogger<WebhookCleanupJob> _logger;

    public WebhookCleanupJob(ShopifyDbContext db, ILogger<WebhookCleanupJob> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var processedCutoff  = DateTimeOffset.UtcNow - ProcessedRetention;
        var deadLetterCutoff = DateTimeOffset.UtcNow - DeadLetterRetention;

        var deletedProcessed = await _db.WebhookEvents
            .Where(e => (e.Status == "processed" || e.Status == "skipped")
                     && e.ProcessedAt < processedCutoff)
            .ExecuteDeleteAsync(ct);

        var deletedDead = await _db.WebhookEvents
            .Where(e => e.Status == "failed"
                     && e.ErrorMessage!.StartsWith("[DEAD]")
                     && e.ProcessedAt < deadLetterCutoff)
            .ExecuteDeleteAsync(ct);

        _logger.LogInformation(
            "WebhookCleanup: deleted {Processed} processed/skipped event(s) older than {ProcessedDays} days " +
            "and {Dead} dead-letter event(s) older than {DeadDays} days.",
            deletedProcessed, ProcessedRetention.Days,
            deletedDead,      DeadLetterRetention.Days);
    }
}
