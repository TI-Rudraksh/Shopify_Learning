using Newtonsoft.Json;

namespace ShopifyIntegration.Features.Webhooks.Models;

public sealed class OrderWebhook
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("financial_status")]
    public string FinancialStatus { get; set; } = string.Empty;

    [JsonProperty("fulfillment_status")]
    public string? FulfillmentStatus { get; set; }

    [JsonProperty("total_price")]
    public string TotalPrice { get; set; } = "0.00";

    [JsonProperty("currency")]
    public string Currency { get; set; } = string.Empty;

    [JsonProperty("customer")]
    public OrderWebhookCustomer? Customer { get; set; }

    [JsonProperty("line_items")]
    public List<OrderWebhookLineItem> LineItems { get; set; } = [];

    [JsonProperty("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonProperty("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonProperty("cancelled_at")]
    public DateTimeOffset? CancelledAt { get; set; }
}

public sealed class OrderWebhookCustomer
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;

    [JsonProperty("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [JsonProperty("last_name")]
    public string LastName { get; set; } = string.Empty;

    [JsonProperty("phone")]
    public string? Phone { get; set; }

    [JsonProperty("accepts_marketing")]
    public bool AcceptsMarketing { get; set; }

    [JsonProperty("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class OrderWebhookLineItem
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("variant_title")]
    public string? VariantTitle { get; set; }

    [JsonProperty("quantity")]
    public int Quantity { get; set; }

    [JsonProperty("price")]
    public string Price { get; set; } = "0.00";

    [JsonProperty("sku")]
    public string? Sku { get; set; }

    [JsonProperty("product_id")]
    public long? ProductId { get; set; }

    [JsonProperty("variant_id")]
    public long? VariantId { get; set; }
}
