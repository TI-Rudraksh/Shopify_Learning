using ShopifyIntegration.GraphQL.Responses.Products.Shared;

namespace ShopifyIntegration.GraphQL.Responses.Products;


public class GetProductsResponse
{
    public ProductsConnection? Products { get; set; }
}

public class ProductsConnection
{
    public List<ProductEdge>? Edges { get; set; }
}

public class ProductEdge
{
    public ShopifyProduct? Node { get; set; }
}