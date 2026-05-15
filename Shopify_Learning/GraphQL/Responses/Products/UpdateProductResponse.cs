using ShopifyIntegration.GraphQL.Responses.Products.Shared;
using UserError = ShopifySharp.GraphQL.UserError;

namespace ShopifyIntegration.GraphQL.Responses.Products;

public class UpdateProductResponse
{
    public ProductUpdatePayload? ProductUpdate { get; set; }
}

public class ProductUpdatePayload
{
    public ShopifyProduct? Product { get; set; }

    public List<UserError>? UserErrors { get; set; }
}
