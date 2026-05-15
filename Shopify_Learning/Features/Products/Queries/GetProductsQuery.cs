using MediatR;
using ShopifyIntegration.GraphQL.Responses.Products;

namespace ShopifyIntegration.Features.Products.Queries;

public sealed record GetProductsQuery() : IRequest<GetProductsResponse?>;
