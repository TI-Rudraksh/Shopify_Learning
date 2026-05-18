namespace ShopifyIntegration.GraphQL.Mutations;

public static class OrderMutations
{
    public const string OrderUpdate = @"
mutation orderUpdate($input: OrderInput!) {
  orderUpdate(input: $input) {
    order {
      id
      note
      customAttributes {
        key
        value
      }
    }
    userErrors {
      field
      message
    }
  }
}";
}
