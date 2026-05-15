using MediatR;
using ShopifyIntegration.GraphQL.Responses.Products;

namespace ShopifyIntegration.Features.Products.Commands;

public sealed record UpdateProductCommand(string ShopifyGid, string Title, string DescriptionHtml)
    : IRequest<UpdateProductResponse?>;
