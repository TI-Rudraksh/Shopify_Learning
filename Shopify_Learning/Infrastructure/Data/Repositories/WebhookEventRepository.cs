using Microsoft.EntityFrameworkCore;
using ShopifyIntegration.Domain.Entities;
using ShopifyIntegration.Domain.Repositories;

namespace ShopifyIntegration.Infrastructure.Data.Repositories;

public sealed class WebhookEventRepository : IWebhookEventRepository
{
    private readonly ShopifyDbContext _db;

    public WebhookEventRepository(ShopifyDbContext db) => _db = db;

    public async Task<WebhookEvent> AddAsync(WebhookEvent webhookEvent, CancellationToken ct = default)
    {
        _db.WebhookEvents.Add(webhookEvent);
        await _db.SaveChangesAsync(ct);
        return webhookEvent;
    }

    public Task<List<WebhookEvent>> GetByTopicAsync(string topic, CancellationToken ct = default)
        => _db.WebhookEvents.Where(e => e.Topic == topic).ToListAsync(ct);

    public Task<bool> ExistsProcessedAsync(string topic, long shopifyNumericId, CancellationToken ct = default)
        => _db.WebhookEvents
            .AnyAsync(e => e.Topic == topic
                        && e.ShopifyNumericId == shopifyNumericId
                        && e.Status == "processed", ct);
}
