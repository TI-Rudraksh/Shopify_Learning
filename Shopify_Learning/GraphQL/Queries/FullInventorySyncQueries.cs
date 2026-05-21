namespace ShopifyIntegration.GraphQL.Queries;

public static class FullInventorySyncQueries
{
    /// <summary>
    /// Paginates through all products in the store, returning each product's
    /// variants with their inventory item GID and all inventory levels
    /// (quantity + location) across every location.
    ///
    /// Used exclusively by FullInventorySyncJob for the weekly full reconciliation.
    /// </summary>
    public const string GetAllProductInventory = @"
query getAllProductInventory($first: Int!, $after: String) {
  products(first: $first, after: $after) {
    pageInfo {
      hasNextPage
      endCursor
    }
    edges {
      node {
        id
        variants(first: 10) {
          edges {
            node {
              inventoryItem {
                id
                inventoryLevels(first: 20) {
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
          }
        }
      }
    }
  }
}";
}
