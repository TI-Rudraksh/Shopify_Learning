using FluentValidation;
using Hangfire;
using Hangfire.PostgreSql;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ShopifyIntegration.Infrastructure.Data;
using ShopifyIntegration.Infrastructure.Data.Repositories;
using ShopifyIntegration.Infrastructure.Hangfire;
using ShopifyIntegration.Infrastructure.Shopify;
using ShopifyIntegration.Infrastructure.Shopify.Validators;
using ShopifyIntegration.Domain.Repositories;
using ShopifyIntegration.Jobs;
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

// ── Hangfire ──────────────────────────────────────────────────────────────────
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options =>
        options.UseNpgsqlConnection(connectionString)));

builder.Services.AddHangfireServer(options =>
{
    // 4 workers total — split across 3 priority queues.
    // Workers drain "critical" first, then "default", then "maintenance".
    options.WorkerCount  = 4;
    options.Queues       = ["critical", "default", "maintenance"];
    options.ServerName   = "shopify-integration-worker";
});

// Register jobs as scoped so they get DI (ShopifyDbContext, IMediator, etc.)
builder.Services.AddScoped<WebhookReprocessorJob>();
builder.Services.AddScoped<OrderSyncJob>();
builder.Services.AddScoped<InventoryDriftDetectorJob>();
builder.Services.AddScoped<StaleFulfillmentCheckerJob>();
builder.Services.AddScoped<WebhookCleanupJob>();

// ── New Hangfire jobs ─────────────────────────────────────────────────────────
builder.Services.AddScoped<ProcessWebhookJob>();
builder.Services.AddScoped<FulfillOrderJob>();
builder.Services.AddScoped<FulfillOrderLineItemsJob>();
builder.Services.AddScoped<SendFulfillmentNotificationJob>();
builder.Services.AddScoped<FullInventorySyncJob>();
builder.Services.AddScoped<DeadLetterMonitorJob>();

// Named HttpClient for outbound notifications (Slack / Teams / PagerDuty)
builder.Services.AddHttpClient("notifications");

// Dashboard auth filter (reads Hangfire:DashboardKey from config)
builder.Services.AddSingleton<HangfireDashboardAuthFilter>();

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

// ── Hangfire Dashboard & Recurring Jobs ───────────────────────────────────────
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    // Auth filter: access via /hangfire?key=<Hangfire:DashboardKey>
    // A session cookie is set so subsequent requests don't need the key.
    Authorization = [app.Services.GetRequiredService<HangfireDashboardAuthFilter>()],

    // Prevent accidental job deletion / re-queuing in production.
    // Set to false if you need to manually trigger jobs from the dashboard.
    IsReadOnlyFunc = _ => !app.Environment.IsDevelopment(),
});

// Register all recurring jobs
var jobs = app.Services.GetRequiredService<IRecurringJobManager>();

// ── Existing recurring jobs ───────────────────────────────────────────────────

// Job 1: Retry failed webhooks — every 5 minutes (maintenance queue)
jobs.AddOrUpdate<WebhookReprocessorJob>(
    "webhook-reprocessor",
    "maintenance",
    job => job.ExecuteAsync(CancellationToken.None),
    "*/5 * * * *");

// Job 2: Pull recent orders from Shopify — every 15 minutes (default queue)
jobs.AddOrUpdate<OrderSyncJob>(
    "order-sync",
    "default",
    job => job.ExecuteAsync(CancellationToken.None),
    "*/15 * * * *");

// Job 3: Detect and correct inventory drift — every 30 minutes (default queue)
jobs.AddOrUpdate<InventoryDriftDetectorJob>(
    "inventory-drift-detector",
    "default",
    job => job.ExecuteAsync(CancellationToken.None),
    "*/30 * * * *");

// Job 4: Check fulfillments stuck in pending/in_progress — daily at 03:00 UTC (maintenance)
jobs.AddOrUpdate<StaleFulfillmentCheckerJob>(
    "stale-fulfillment-checker",
    "maintenance",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 3 * * *");

// Job 5: Purge old webhook_events rows — weekly on Sunday at 02:00 UTC (maintenance)
jobs.AddOrUpdate<WebhookCleanupJob>(
    "webhook-cleanup",
    "maintenance",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 2 * * 0");

// ── New recurring jobs ────────────────────────────────────────────────────────

// Job 6: Full inventory reconciliation — weekly on Sunday at 01:00 UTC (maintenance)
// Runs before WebhookCleanupJob so any new inventory items are in the DB first.
jobs.AddOrUpdate<FullInventorySyncJob>(
    "full-inventory-sync",
    "maintenance",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 1 * * 0");

// Job 7: Dead-letter webhook monitor — every hour (default queue)
// Posts an alert to Hangfire:NotificationWebhookUrl if dead-letter events exist.
jobs.AddOrUpdate<DeadLetterMonitorJob>(
    "dead-letter-monitor",
    "default",
    job => job.ExecuteAsync(CancellationToken.None),
    "0 * * * *");

app.UseAuthorization();

app.MapControllers();

app.Run();
