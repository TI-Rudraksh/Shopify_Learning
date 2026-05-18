using ShopifyIntegration.GraphQL.Responses.Fulfillment;

namespace ShopifyIntegration.Infrastructure.Shopify;

public interface IShopifyFulfillmentService
{
    /// <summary>
    /// Fulfills all open fulfillment orders for the given Shopify order GID.
    /// Returns the created fulfillment payload from Shopify.
    /// </summary>
    Task<FulfillmentCreatePayload> FulfillOrderAsync(
        string  orderGid,
        string? trackingNumber  = null,
        string? trackingCompany = null,
        bool    notifyCustomer  = true,
        CancellationToken ct    = default);

    /// <summary>
    /// Fulfills specific line items of an order by matching them to their
    /// FulfillmentOrder line items and calling the fulfillmentCreate mutation.
    /// Returns the created fulfillment payload from Shopify.
    /// </summary>
    Task<FulfillmentCreatePayload> FulfillLineItemsAsync(
        string            orderGid,
        List<string>      lineItemGids,
        string?           trackingNumber  = null,
        string?           trackingCompany = null,
        bool              notifyCustomer  = true,
        CancellationToken ct              = default);
}
