using Newtonsoft.Json;

namespace ShopifyIntegration.Features.Webhooks.Models;

public sealed class ProductDeletedWebhook
{
    [JsonProperty("id")]
    public long Id { get; set; }
}
