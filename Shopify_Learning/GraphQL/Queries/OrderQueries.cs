namespace ShopifyIntegration.GraphQL.Queries;

public static class OrderQueries
{
    public const string GetOrderNote = @"
query getOrderNote($orderId: ID!) {
  order(id: $orderId) {
    id
    note
    customAttributes {
      key
      value
    }
  }
}";
}
