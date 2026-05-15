namespace ShopifyIntegration.GraphQL.Mutations;

public static class InventoryMutations
{
    public const string SetOnHandQuantities = @"
mutation inventorySetOnHandQuantities($input: InventorySetOnHandQuantitiesInput!) {
  inventorySetOnHandQuantities(input: $input) {
    inventoryAdjustmentGroup {
      reason
      changes {
        name
        delta
        quantityAfterChange
        item {
          id
        }
        location {
          id
          name
        }
      }
    }
    userErrors {
      field
      message
      code
    }
  }
}";

    public const string ActivateInventoryItem = @"
mutation inventoryBulkToggleActivation($inventoryItemId: ID!, $inventoryItemUpdates: [InventoryBulkToggleActivationInput!]!) {
  inventoryBulkToggleActivation(inventoryItemId: $inventoryItemId, inventoryItemUpdates: $inventoryItemUpdates) {
    inventoryItem {
      id
    }
    inventoryLevels {
      id
      location {
        id
      }
    }
    userErrors {
      field
      message
      code
    }
  }
}";
}
