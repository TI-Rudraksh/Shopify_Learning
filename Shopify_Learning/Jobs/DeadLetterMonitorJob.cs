using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShopifyIntegration.Infrastructure.Data;

namespace ShopifyIntegration.Jobs;

/// <summary>
/// Checks for dead-lettered webhook events (status="failed" with "[DEAD]" prefix)
/// and posts an alert to a configured notification webhook URL.
///
/// Runs every hour. If no dead-letter events exist, exits immediately.
/// If Hangfire:NotificationWebhookUrl is not configured, logs a warning and exits.
///
/// This replaces the silent failure mode where dead-letter events accumulate
/// unnoticed until someone manually queries the DB or checks the dashboard.
/// </summary>
public sealed class DeadLetterMonitorJob
{
    private readonly ShopifyDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<DeadLetterMonitorJob> _logger;

    public DeadLetterMonitorJob(
        ShopifyDbContext db,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<DeadLetterMonitorJob> logger)
    {
        _db                = db;
        _httpClientFactory = httpClientFactory;
        _config            = config;
        _logger            = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var deadEvents = await _db.WebhookEvents
            .AsNoTracking()
            .Where(e => e.Status == "failed"
                     && e.ErrorMessage != null
                     && e.ErrorMessage.StartsWith("[DEAD]"))
            .Select(e => new { e.Id, e.Topic, e.ProcessedAt, e.ErrorMessage })
            .OrderByDescending(e => e.ProcessedAt)
            .Take(20)
            .ToListAsync(ct);

        if (deadEvents.Count == 0)
        {
            _logger.LogInformation("DeadLetterMonitor: no dead-letter events found.");
            return;
        }

        _logger.LogWarning(
            "DeadLetterMonitor: {Count} dead-letter webhook event(s) found.", deadEvents.Count);

        var webhookUrl = _config["Hangfire:NotificationWebhookUrl"];
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            _logger.LogWarning(
                "DeadLetterMonitor: Hangfire:NotificationWebhookUrl is not configured. " +
                "Cannot send alert. Dead-letter events: {Count}.", deadEvents.Count);
            return;
        }

        // Build a summary of the most recent dead-letter events
        var topicSummary = deadEvents
            .GroupBy(e => e.Topic)
            .Select(g => $"{g.Key} ×{g.Count()}")
            .ToList();

        var message = $"⚠️ Shopify Integration — {deadEvents.Count} dead-letter webhook(s) need manual review.\n" +
                      $"Topics: {string.Join(", ", topicSummary)}\n" +
                      $"Check the Hangfire dashboard or query webhook_events WHERE status='failed' AND error_message LIKE '[DEAD]%'.";

        var payload = JsonSerializer.Serialize(new { text = message });

        try
        {
            var client   = _httpClientFactory.CreateClient("notifications");
            var content  = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(webhookUrl, content, ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "DeadLetterMonitor: alert sent for {Count} dead-letter event(s).", deadEvents.Count);
            }
            else
            {
                _logger.LogWarning(
                    "DeadLetterMonitor: alert POST returned {StatusCode}.",
                    (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            // Don't let a notification failure crash the monitoring job itself
            _logger.LogError(ex, "DeadLetterMonitor: failed to send alert.");
        }
    }
}
