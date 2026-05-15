using Newtonsoft.Json;

namespace ShopifyIntegration.Features.Webhooks.Models;

public sealed class FulfillmentWebhook
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("order_id")]
    public long OrderId { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty("tracking_number")]
    public string? TrackingNumber { get; set; }

    [JsonProperty("tracking_company")]
    public string? TrackingCompany { get; set; }

    [JsonProperty("tracking_url")]
    public string? TrackingUrl { get; set; }

    [JsonProperty("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonProperty("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}
