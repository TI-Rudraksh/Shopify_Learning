using ShopifyIntegration.GraphQL.Responses.Products.Shared;
using UserError = ShopifySharp.GraphQL.UserError;

namespace ShopifyIntegration.GraphQL.Responses.Products;

public class CreateProductResponse
{
    public ProductCreatePayload? ProductCreate { get; set; }
}

public class ProductCreatePayload
{
    public ShopifyProduct? Product { get; set; }

    public List<UserError>? UserErrors { get; set; }
}
