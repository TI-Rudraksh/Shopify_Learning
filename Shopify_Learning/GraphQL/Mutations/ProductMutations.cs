namespace ShopifyIntegration.GraphQL.Mutations;

public static class ProductMutations
{
    public const string CreateProduct = @"
mutation productCreate($input: ProductCreateInput!) {
  productCreate(product: $input) {
    product {
      id
      title
      vendor
    }
    userErrors {
      field
      message
    }
  }
}";
    
    public const string UpdateProduct = @"
mutation productUpdate($input: ProductUpdateInput!) {
  productUpdate(product: $input) {
    product {
      id
      title
      vendor
    }
    userErrors {
      field
      message
    }
  }
}";

    public const string DeleteProduct = @"
mutation productDelete($input: ProductDeleteInput!) {
  productDelete(input: $input) {
    deletedProductId
    userErrors {
      field
      message
    }
  }
}";
}