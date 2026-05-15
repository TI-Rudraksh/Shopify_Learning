using MediatR;
using ShopifyIntegration.GraphQL.Responses.Products;

namespace ShopifyIntegration.Features.Products.Commands;

public sealed record CreateProductCommand(string Title, string DescriptionHtml, string Vendor)
    : IRequest<CreateProductResponse?>;
