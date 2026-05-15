using Newtonsoft.Json;

namespace ShopifyIntegration.Features.Webhooks.Models;

public sealed class LocationDeletedWebhook
{
    [JsonProperty("id")]
    public long Id { get; set; }
}
