namespace ShopifyIntegration.GraphQL.Queries;

public static class OrderSyncQueries
{
    /// <summary>
    /// NOTE: Shopify's `query:` argument on the orders connection does NOT support
    /// GraphQL variables — it only accepts an inline string literal.
    /// This constant is kept as documentation of the query shape.
    /// The actual query is built dynamically in OrderSyncJob.ExecuteAsync()
    /// with the filter string embedded directly in the GQL text.
    /// </summary>
    public const string GetRecentOrders = @"
{
  orders(first: 25, sortKey: UPDATED_AT, reverse: true,
         query: ""updated_at:>=<INLINE_FILTER>"") {
    pageInfo {
      hasNextPage
      endCursor
    }
    edges {
      node {
        id
        name
        displayFinancialStatus
        displayFulfillmentStatus
        totalPriceSet { shopMoney { amount currencyCode } }
        note
        customAttributes { key value }
        createdAt
        updatedAt
        cancelledAt
        customer { id }
        lineItems(first: 50) {
          edges {
            node {
              id
              title
              variantTitle
              quantity
              originalUnitPriceSet { shopMoney { amount } }
              sku
              product { id }
              variant { id }
            }
          }
        }
      }
    }
  }
}";
}
