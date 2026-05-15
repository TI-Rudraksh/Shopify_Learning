using MediatR;
using ShopifyIntegration.GraphQL.Responses.Products;
using ShopifyIntegration.Infrastructure.Shopify;

namespace ShopifyIntegration.Features.Products.Commands;

public sealed class DeleteProductCommandHandler
    : IRequestHandler<DeleteProductCommand, DeleteProductResponse?>
{
    private readonly IShopifyGraphQLService _shopify;

    public DeleteProductCommandHandler(IShopifyGraphQLService shopify)
        => _shopify = shopify;

    public Task<DeleteProductResponse?> Handle(
        DeleteProductCommand command, CancellationToken cancellationToken)
        => _shopify.DeleteProductAsync(command.ShopifyGid, cancellationToken);
}
