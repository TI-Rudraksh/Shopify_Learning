using ShopifyIntegration.GraphQL.Responses.Shared;

namespace ShopifyIntegration.GraphQL.Responses.Products;

public class DeleteProductResponse
{
    public ProductDeletePayload? ProductDelete { get; set; }
}

public class ProductDeletePayload
{
    public string?                  DeletedProductId { get; set; }
    public List<GraphQLUserError>?  UserErrors       { get; set; }
}
