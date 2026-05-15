namespace ShopifyIntegration.GraphQL.Queries;

public static class InventoryQueries
{
    public const string GetInventoryItemGid = @"
query getInventoryItemGid($id: ID!) {
  product(id: $id) {
    variants(first: 1) {
      edges {
        node {
          inventoryItem {
            id
          }
        }
      }
    }
  }
}";
}
