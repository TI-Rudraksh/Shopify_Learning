namespace ShopifyIntegration.GraphQL.Mutations;

public static class FulfillmentMutations
{
    public const string FulfillmentCreate = @"
mutation fulfillmentCreate($fulfillment: FulfillmentInput!) {
  fulfillmentCreate(fulfillment: $fulfillment) {
    fulfillment {
      id
      status
      createdAt
      updatedAt
      trackingInfo {
        number
        company
        url
      }
    }
    userErrors {
      field
      message
    }
  }
}";
}
