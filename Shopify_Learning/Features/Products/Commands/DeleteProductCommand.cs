using MediatR;
using ShopifyIntegration.GraphQL.Responses.Products;

namespace ShopifyIntegration.Features.Products.Commands;

public sealed record DeleteProductCommand(string ShopifyGid)
    : IRequest<DeleteProductResponse?>;
