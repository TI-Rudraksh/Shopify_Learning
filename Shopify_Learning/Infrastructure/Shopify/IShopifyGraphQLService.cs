using ShopifyIntegration.DTOs;
using ShopifyIntegration.GraphQL.Responses.Products;

namespace ShopifyIntegration.Infrastructure.Shopify;

public interface IShopifyGraphQLService
{
    Task<CreateProductResponse?> CreateProductAsync(CreateProductGraphQLDto dto, CancellationToken ct = default);
    Task<GetProductsResponse?> GetProductsAsync(CancellationToken ct = default);
    Task<UpdateProductResponse?> UpdateProductAsync(UpdateProductGraphQLDto dto, CancellationToken ct = default);
    Task<DeleteProductResponse?> DeleteProductAsync(string productId, CancellationToken ct = default);
}
