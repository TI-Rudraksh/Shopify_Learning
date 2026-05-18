using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopifyIntegration.Infrastructure.Data;
using ShopifyIntegration.Infrastructure.Data.Repositories;
using ShopifyIntegration.Infrastructure.Shopify;
using ShopifyIntegration.Infrastructure.Shopify.Validators;
using ShopifyIntegration.Domain.Repositories;
using ShopifyIntegration.Middleware;
using ShopifyIntegration.Models;
using ShopifyIntegration.Pipeline;

var builder = WebApplication.CreateBuilder(args);

// Validate connection string before building the container
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is missing or empty. " +
        "Set it in appsettings.json or an environment variable.");

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));

builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

builder.Services.Configure<ShopifySettings>(
    builder.Configuration.GetSection("Shopify"));

builder.Services.AddHttpClient<ShopifyGraphQLService>();
builder.Services.AddScoped<IShopifyGraphQLService, ShopifyGraphQLService>();

builder.Services.AddScoped<IShopifyWebhookValidator, ShopifyWebhookValidator>();

builder.Services.AddDbContext<ShopifyDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
        npgsql.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null)));

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IWebhookEventRepository, WebhookEventRepository>();
builder.Services.AddScoped<IShopifyInventoryService, ShopifyInventoryService>();
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<ILocationRepository, LocationRepository>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IFulfillmentRepository, FulfillmentRepository>();
builder.Services.AddScoped<IShopifyFulfillmentService, ShopifyFulfillmentService>();
builder.Services.AddScoped<IShopifyOrderService, ShopifyOrderService>();

var app = builder.Build();

// Run pending migrations at startup; terminate if migration fails
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<ShopifyDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Database migration failed. Application will terminate.");
        Environment.Exit(1);
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseAuthorization();

app.MapControllers();

app.Run();
