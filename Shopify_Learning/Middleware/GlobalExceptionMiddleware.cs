using FluentValidation;
using ShopifyIntegration.Infrastructure.Shopify;

namespace ShopifyIntegration.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            context.Response.StatusCode  = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            var errors = ex.Errors.Select(e => e.ErrorMessage);
            await context.Response.WriteAsJsonAsync(new { errors });
        }
        catch (ShopifyInventoryException ex)
        {
            context.Response.StatusCode  = StatusCodes.Status502BadGateway;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { errors = ex.ShopifyErrors });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode  = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(
                new { error = "An unexpected error occurred." });
        }
    }
}
