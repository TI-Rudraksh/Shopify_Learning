using Newtonsoft.Json;

namespace ShopifyIntegration.Features.Webhooks.Models;

/// <summary>
/// Payload shape for the inventory_levels/update and inventory_levels/connect
/// Shopify webhook topics.
///
/// Shopify fires inventory_levels/update whenever available quantity changes
/// at a location — sales, manual adjustments, returns, transfers, etc.
/// This is the primary real-time signal for keeping inventory_levels in sync.
///
/// Key fields:
///   inventory_item_id — numeric ID of the inventory item (variant-level)
///   location_id       — numeric ID of the location
///   available         — current available quantity (can be null if not tracked)
///   updated_at        — timestamp of the change in Shopify
/// </summary>
public sealed class InventoryLevelWebhook
{
    [JsonProperty("inventory_item_id")]
    public long InventoryItemId { get; set; }

    [JsonProperty("location_id")]
    public long LocationId { get; set; }

    [JsonProperty("available")]
    public int? Available { get; set; }

    [JsonProperty("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}
