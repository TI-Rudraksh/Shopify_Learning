using MediatR;
using ShopifyIntegration.DTOs;
using ShopifyIntegration.GraphQL.Responses.Products;
using ShopifyIntegration.Infrastructure.Shopify;

namespace ShopifyIntegration.Features.Products.Commands;

public sealed class UpdateProductCommandHandler
    : IRequestHandler<UpdateProductCommand, UpdateProductResponse?>
{
    private readonly IShopifyGraphQLService _shopify;

    public UpdateProductCommandHandler(IShopifyGraphQLService shopify)
        => _shopify = shopify;

    public Task<UpdateProductResponse?> Handle(
        UpdateProductCommand command, CancellationToken cancellationToken)
        => _shopify.UpdateProductAsync(
            new UpdateProductGraphQLDto
            {
                Id              = command.ShopifyGid,
                Title           = command.Title,
                DescriptionHtml = command.DescriptionHtml
            },
            cancellationToken);
}
