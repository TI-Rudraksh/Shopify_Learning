using Microsoft.EntityFrameworkCore;
using ShopifyIntegration.Domain.Entities;

namespace ShopifyIntegration.Infrastructure.Data;

public sealed class ShopifyDbContext : DbContext
{
    public ShopifyDbContext(DbContextOptions<ShopifyDbContext> options)
        : base(options) { }

    public DbSet<Product>        Products        { get; set; } = null!;
    public DbSet<WebhookEvent>   WebhookEvents   { get; set; } = null!;
    public DbSet<InventoryLevel> InventoryLevels { get; set; } = null!;
    public DbSet<Location>       Locations       { get; set; } = null!;
    public DbSet<Customer>       Customers       { get; set; } = null!;
    public DbSet<Order>          Orders          { get; set; } = null!;
    public DbSet<OrderLineItem>  OrderLineItems  { get; set; } = null!;
    public DbSet<Fulfillment>    Fulfillments    { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ShopifyDbContext).Assembly);
    }
}
