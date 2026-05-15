using Newtonsoft.Json;

namespace ShopifyIntegration.Features.Webhooks.Models;

public sealed class ProductUpdatedWebhook
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("vendor")]
    public string Vendor { get; set; } = string.Empty;

    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}
