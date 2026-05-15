using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShopifyIntegration.Features.Products.Commands;
using ShopifyIntegration.Features.Products.Queries;

namespace ShopifyIntegration.API.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductsController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<IActionResult> CreateProduct(
        [FromBody] CreateProductCommand command, CancellationToken ct)
        => Ok(await _mediator.Send(command, ct));

    [HttpGet]
    public async Task<IActionResult> GetProducts(CancellationToken ct)
        => Ok(await _mediator.Send(new GetProductsQuery(), ct));

    [HttpGet("{gid}")]
    public async Task<IActionResult> GetProductById(string gid, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProductByIdQuery(gid), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateProduct(
        [FromBody] UpdateProductCommand command, CancellationToken ct)
        => Ok(await _mediator.Send(command, ct));

    [HttpDelete]
    public async Task<IActionResult> DeleteProduct(
        [FromQuery] string gid, CancellationToken ct)
        => Ok(await _mediator.Send(new DeleteProductCommand(gid), ct));
}
