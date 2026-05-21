namespace ShopifyIntegration.GraphQL.Queries;

public static class FulfillmentStatusQueries
{
    /// <summary>
    /// Fetches the current status of a fulfillment from Shopify.
    /// Used by the StaleFulfillmentCheckerJob to detect fulfillments
    /// that are stuck in a pending/in-progress state locally.
    /// </summary>
    public const string GetFulfillmentStatus = @"
query getFulfillmentStatus($id: ID!) {
  fulfillment(id: $id) {
    id
    status
    updatedAt
  }
}";
}
