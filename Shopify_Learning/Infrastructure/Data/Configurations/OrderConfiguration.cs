using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopifyIntegration.Domain.Entities;

namespace ShopifyIntegration.Infrastructure.Data.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(o => o.ShopifyGid).HasColumnName("shopify_gid").IsRequired();
        builder.Property(o => o.NumericId).HasColumnName("numeric_id").IsRequired();
        builder.Property(o => o.Name).HasColumnName("name").IsRequired();
        builder.Property(o => o.FinancialStatus).HasColumnName("financial_status").IsRequired();
        builder.Property(o => o.FulfillmentStatus).HasColumnName("fulfillment_status").IsRequired();
        builder.Property(o => o.TotalPrice).HasColumnName("total_price").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(o => o.Currency).HasColumnName("currency").IsRequired();
        builder.Property(o => o.CustomerId).HasColumnName("customer_id");
        builder.Property(o => o.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(o => o.CancelledAt).HasColumnName("cancelled_at");
        builder.HasIndex(o => o.ShopifyGid).IsUnique();
        builder.HasOne(o => o.Customer)
               .WithMany()
               .HasForeignKey(o => o.CustomerId)
               .IsRequired(false)
               .HasConstraintName("fk_orders_customers");
        builder.HasMany(o => o.LineItems)
               .WithOne(li => li.Order)
               .HasForeignKey(li => li.OrderId)
               .HasConstraintName("fk_order_line_items_orders");
        builder.HasMany(o => o.Fulfillments)
               .WithOne(f => f.Order)
               .HasForeignKey(f => f.OrderId)
               .HasConstraintName("fk_fulfillments_orders");
    }
}
