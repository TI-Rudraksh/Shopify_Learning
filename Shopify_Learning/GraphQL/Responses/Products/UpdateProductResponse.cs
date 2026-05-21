using ShopifyIntegration.GraphQL.Responses.Products.Shared;
using ShopifyIntegration.GraphQL.Responses.Shared;

namespace ShopifyIntegration.GraphQL.Responses.Products;

public class UpdateProductResponse
{
    public ProductUpdatePayload? ProductUpdate { get; set; }
}

public class ProductUpdatePayload
{
    public ShopifyProduct?         Product    { get; set; }
    public List<GraphQLUserError>? UserErrors { get; set; }
}
