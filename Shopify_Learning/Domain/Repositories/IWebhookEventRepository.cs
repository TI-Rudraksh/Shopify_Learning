using ShopifyIntegration.Domain.Entities;

namespace ShopifyIntegration.Domain.Repositories;

public interface IWebhookEventRepository
{
    Task<WebhookEvent>       AddAsync(WebhookEvent webhookEvent, CancellationToken ct = default);
    Task<List<WebhookEvent>> GetByTopicAsync(string topic, CancellationToken ct = default);

    // Idempotency check: returns true if a "processed" event already exists for this topic + numeric id
    Task<bool> ExistsProcessedAsync(string topic, long shopifyNumericId, CancellationToken ct = default);
}
