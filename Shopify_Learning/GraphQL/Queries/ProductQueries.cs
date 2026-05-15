namespace ShopifyIntegration.GraphQL.Queries;

public static class ProductQueries
{
    public const string GetProducts = @"
query {
  products(first: 10, sortKey: CREATED_AT, reverse: true) {
    edges {
      node {
        id
        title
        vendor
      }
    }
  }
}";
}