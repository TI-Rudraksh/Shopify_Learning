using ShopifySharp;
using ShopifySharp.Services.Graph;
using ShopifyIntegration.Infrastructure.Data.Helpers;
using ShopifyIntegration.Domain.Repositories;
using ProductEntity = ShopifyIntegration.Domain.Entities.Product;
using ShopifyIntegration.DTOs;
using ShopifyIntegration.GraphQL.Mutations;
using ShopifyIntegration.GraphQL.Queries;
using ShopifyIntegration.GraphQL.Responses.Products;
using ShopifyIntegration.GraphQL.Responses.Products.Shared;

namespace ShopifyIntegration.Infrastructure.Shopify;

public class ShopifyGraphQLService : IShopifyGraphQLService
{
    private readonly GraphService _graphService;
    private readonly IProductRepository _productRepository;
    private readonly ILogger<ShopifyGraphQLService> _logger;

    public ShopifyGraphQLService(
        IConfiguration configuration,
        IProductRepository productRepository,
        ILogger<ShopifyGraphQLService> logger)
    {
        var shopUrl = configuration["Shopify:StoreUrl"];
        var accessToken = configuration["Shopify:AccessToken"];

        _graphService = new GraphService(
            shopUrl,
            accessToken);

        _productRepository = productRepository;
        _logger = logger;
    }

    private static ProductEntity MapToEntity(ShopifyProduct p)
    {
        var gid = p.Id ?? "";
        return new ProductEntity
        {
            ShopifyGid = gid,
            NumericId  = string.IsNullOrEmpty(gid) ? 0 : ShopifyGidHelper.ParseNumericId(gid),
            Title      = p.Title  ?? "",
            Vendor     = p.Vendor ?? "",
            Status     = "",
            CreatedAt  = DateTimeOffset.UtcNow,
            UpdatedAt  = DateTimeOffset.UtcNow,
        };
    }

    private async Task<T?> ExecuteAsync<T>(
        string query,
        Dictionary<string, object>? variables = null)
    {
        var request = new GraphRequest
        {
            Query = query,
            Variables = variables
        };

        var response =
            await _graphService.PostAsync<T>(request);

        return response.Data;
    }

    // CREATE
    public async Task<CreateProductResponse?> CreateProductAsync(
        CreateProductGraphQLDto dto,
        CancellationToken ct = default)
    {
        var variables = new Dictionary<string, object>
        {
            ["input"] = new Dictionary<string, object>
            {
                ["title"] = dto.Title,
                ["descriptionHtml"] = dto.DescriptionHtml,
                ["vendor"] = dto.Vendor
            }
        };

        var response = await ExecuteAsync<CreateProductResponse>(
            ProductMutations.CreateProduct,
            variables);

        if (response?.ProductCreate?.Product is { } p)
        {
            try
            {
                var entity = MapToEntity(p);
                await _productRepository.UpsertAsync(entity, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist created product to the database.");
                throw;
            }
        }

        return response;
    }

    // READ
    public async Task<GetProductsResponse?> GetProductsAsync(CancellationToken ct = default)
    {
        return await ExecuteAsync<GetProductsResponse>(
            ProductQueries.GetProducts);
    }

    // UPDATE
    public async Task<UpdateProductResponse?> UpdateProductAsync(
        UpdateProductGraphQLDto dto,
        CancellationToken ct = default)
    {
        var variables = new Dictionary<string, object>
        {
            ["input"] = new Dictionary<string, object>
            {
                ["id"] = dto.Id,
                ["title"] = dto.Title,
                ["descriptionHtml"] = dto.DescriptionHtml
            }
        };

        var response = await ExecuteAsync<UpdateProductResponse>(
            ProductMutations.UpdateProduct,
            variables);

        if (response?.ProductUpdate?.Product is { } p)
        {
            try
            {
                var entity = MapToEntity(p);
                await _productRepository.UpsertAsync(entity, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist updated product to the database.");
                throw;
            }
        }

        return response;
    }

    // DELETE
    public async Task<DeleteProductResponse?> DeleteProductAsync(
        string productId,
        CancellationToken ct = default)
    {
        var variables = new Dictionary<string, object>
        {
            ["input"] = new Dictionary<string, object>
            {
                ["id"] = productId
            }
        };

        var response = await ExecuteAsync<DeleteProductResponse>(
            ProductMutations.DeleteProduct,
            variables);

        if (response?.ProductDelete?.DeletedProductId is not null)
        {
            try
            {
                var numericId = ShopifyGidHelper.ParseNumericId(productId);
                await _productRepository.DeleteByNumericIdAsync(numericId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete product from the database.");
                throw;
            }
        }

        return response;
    }
}
