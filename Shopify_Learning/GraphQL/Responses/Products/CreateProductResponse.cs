using ShopifyIntegration.GraphQL.Responses.Products.Shared;
using ShopifyIntegration.GraphQL.Responses.Shared;

namespace ShopifyIntegration.GraphQL.Responses.Products;

public class CreateProductResponse
{
    public ProductCreatePayload? ProductCreate { get; set; }
}

public class ProductCreatePayload
{
    public ShopifyProduct?         Product    { get; set; }
    public List<GraphQLUserError>? UserErrors { get; set; }
}
