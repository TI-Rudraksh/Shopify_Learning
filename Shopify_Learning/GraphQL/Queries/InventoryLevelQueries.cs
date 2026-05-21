namespace ShopifyIntegration.GraphQL.Queries;

public static class InventoryLevelQueries
{
    /// <summary>
    /// Fetches current inventory quantities from Shopify for a batch of inventory item GIDs.
    /// Used by the InventoryDriftDetectorJob to compare against local DB values.
    /// </summary>
    public const string GetInventoryLevels = @"
query getInventoryLevels($ids: [ID!]!) {
  nodes(ids: $ids) {
    ... on InventoryItem {
      id
      inventoryLevels(first: 10) {
        edges {
          node {
            location { id }
            quantities(names: [""available""]) {
              name
              quantity
            }
          }
        }
      }
    }
  }
}";
}
