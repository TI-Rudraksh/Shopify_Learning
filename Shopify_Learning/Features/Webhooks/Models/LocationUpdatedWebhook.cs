using Newtonsoft.Json;

namespace ShopifyIntegration.Features.Webhooks.Models;

public sealed class LocationUpdatedWebhook
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}
