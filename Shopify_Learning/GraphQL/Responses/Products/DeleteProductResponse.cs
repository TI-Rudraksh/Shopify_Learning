using ShopifySharp.GraphQL;

namespace ShopifyIntegration.GraphQL.Responses.Products;

public class DeleteProductResponse
{
    public ProductDeletePayload? ProductDelete { get; set; }
}

public class ProductDeletePayload
{
    public string? DeletedProductId { get; set; }

    public List<UserError>? UserErrors { get; set; }
}
