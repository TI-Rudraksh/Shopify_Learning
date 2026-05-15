using MediatR;
using ShopifyIntegration.DTOs;
using ShopifyIntegration.GraphQL.Responses.Products;
using ShopifyIntegration.Infrastructure.Shopify;

namespace ShopifyIntegration.Features.Products.Commands;

public sealed class CreateProductCommandHandler
    : IRequestHandler<CreateProductCommand, CreateProductResponse?>
{
    private readonly IShopifyGraphQLService _shopify;

    public CreateProductCommandHandler(IShopifyGraphQLService shopify)
        => _shopify = shopify;

    public Task<CreateProductResponse?> Handle(
        CreateProductCommand command, CancellationToken cancellationToken)
        => _shopify.CreateProductAsync(
            new CreateProductGraphQLDto
            {
                Title           = command.Title,
                DescriptionHtml = command.DescriptionHtml,
                Vendor          = command.Vendor
            },
            cancellationToken);
}
