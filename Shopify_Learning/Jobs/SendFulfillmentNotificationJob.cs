using System.Text;
using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using ShopifyIntegration.Infrastructure.Data;

namespace ShopifyIntegration.Jobs;

/// <summary>
/// Sends an outbound notification after a fulfillment job succeeds.
/// Wired as a Hangfire continuation so it only runs when the parent
/// FulfillOrderJob or FulfillOrderLineItemsJob completes successfully.
///
/// Notification target is configured via Hangfire:NotificationWebhookUrl
/// (Slack incoming webhook, Teams connector, PagerDuty, etc.).
/// If the URL is not configured the job logs a warning and exits cleanly —
/// it will not fail the continuation chain.
///
/// Payload sent:
/// {
///   "text": "✅ Order #1234 fulfilled — fulfillment gid://shopify/Fulfillment/… (success)"
/// }
/// </summary>
public sealed class SendFulfillmentNotificationJob
{
    private readonly ShopifyDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<SendFulfillmentNotificationJob> _logger;

    public SendFulfillmentNotificationJob(
        ShopifyDbContext db,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<SendFulfillmentNotificationJob> logger)
    {
        _db                = db;
        _httpClientFactory = httpClientFactory;
        _config            = config;
        _logger            = logger;
    }

    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [30, 120])]
    public async Task ExecuteAsync(string orderId, CancellationToken ct)
    {
        var webhookUrl = _config["Hangfire:NotificationWebhookUrl"];
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            _logger.LogWarning(
                "SendFulfillmentNotificationJob: Hangfire:NotificationWebhookUrl is not configured. " +
                "Skipping notification for order {OrderId}.", orderId);
            return;
        }

        // Resolve the order name for a human-readable message
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Fulfillments)
            .FirstOrDefaultAsync(
                o => o.ShopifyGid == orderId
                  || o.NumericId.ToString() == orderId
                  || o.Id.ToString() == orderId,
                ct);

        if (order is null)
        {
            _logger.LogWarning(
                "SendFulfillmentNotificationJob: order {OrderId} not found in local DB. " +
                "Sending generic notification.", orderId);
        }

        var orderName        = order?.Name ?? orderId;
        var latestFulfillment = order?.Fulfillments
            .OrderByDescending(f => f.UpdatedAt)
            .FirstOrDefault();

        var message = latestFulfillment is not null
            ? $"✅ Order {orderName} fulfilled — {latestFulfillment.ShopifyGid} ({latestFulfillment.Status})"
            : $"✅ Order {orderName} fulfillment completed.";

        var payload = JsonSerializer.Serialize(new { text = message });

        var client   = _httpClientFactory.CreateClient("notifications");
        var content  = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(webhookUrl, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "SendFulfillmentNotificationJob: notification POST returned {StatusCode} for order {OrderId}.",
                (int)response.StatusCode, orderId);

            // Throw so Hangfire retries
            response.EnsureSuccessStatusCode();
        }

        _logger.LogInformation(
            "SendFulfillmentNotificationJob: notification sent for order {OrderId}.", orderId);
    }
}
