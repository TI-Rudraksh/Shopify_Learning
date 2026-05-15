using MediatR;
using ShopifyIntegration.Domain.Entities;
using ShopifyIntegration.Domain.Repositories;

namespace ShopifyIntegration.Features.Products.Queries;

public sealed class GetProductByIdQueryHandler
    : IRequestHandler<GetProductByIdQuery, Product?>
{
    private readonly IProductRepository _products;

    public GetProductByIdQueryHandler(IProductRepository products)
        => _products = products;

    public Task<Product?> Handle(
        GetProductByIdQuery query, CancellationToken cancellationToken)
        => _products.GetByShopifyGidAsync(query.ShopifyGid, cancellationToken);
}
