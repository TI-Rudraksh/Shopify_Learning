using MediatR;
using ShopifyIntegration.Domain.Entities;

namespace ShopifyIntegration.Features.Products.Queries;

public sealed record GetProductByIdQuery(string ShopifyGid) : IRequest<Product?>;
