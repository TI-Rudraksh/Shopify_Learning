using ShopifyIntegration.GraphQL.Responses.Orders;

namespace ShopifyIntegration.Infrastructure.Shopify;

public interface IShopifyOrderService
{
    /// <summary>
    /// Fetches the note and note attributes for a Shopify order by GID.
    /// </summary>
    Task<ShopifyOrderNode?> GetOrderNoteAsync(
        string            orderGid,
        CancellationToken ct = default);

    /// <summary>
    /// Updates the note and/or note attributes (customAttributes) on a Shopify order.
    /// Returns the updated order payload from Shopify.
    /// </summary>
    Task<OrderUpdatePayload> UpdateOrderNoteAsync(
        string                                    orderGid,
        string?                                   note,
        IEnumerable<(string Name, string Value)>? noteAttributes,
        CancellationToken                         ct = default);
}
