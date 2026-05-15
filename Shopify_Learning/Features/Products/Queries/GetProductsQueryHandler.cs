using MediatR;
using ShopifyIntegration.GraphQL.Responses.Products;
using ShopifyIntegration.Infrastructure.Shopify;

namespace ShopifyIntegration.Features.Products.Queries;

public sealed class GetProductsQueryHandler
    : IRequestHandler<GetProductsQuery, GetProductsResponse?>
{
    private readonly IShopifyGraphQLService _shopify;

    public GetProductsQueryHandler(IShopifyGraphQLService shopify)
        => _shopify = shopify;

    public Task<GetProductsResponse?> Handle(
        GetProductsQuery query, CancellationToken cancellationToken)
        => _shopify.GetProductsAsync(cancellationToken);
}
