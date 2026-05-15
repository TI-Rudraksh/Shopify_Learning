namespace ShopifyIntegration.GraphQL.Queries;

public static class FulfillmentQueries
{
    public const string GetFulfillmentOrders = @"
query getFulfillmentOrders($orderId: ID!) {
  order(id: $orderId) {
    fulfillmentOrders(first: 10) {
      edges {
        node {
          id
          status
        }
      }
    }
  }
}";
}
