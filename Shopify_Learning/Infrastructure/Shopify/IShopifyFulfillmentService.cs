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
}
