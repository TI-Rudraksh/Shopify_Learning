using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopifyIntegration.Domain.Entities;

namespace ShopifyIntegration.Infrastructure.Data.Configurations;

public sealed class OrderLineItemConfiguration : IEntityTypeConfiguration<OrderLineItem>
{
    public void Configure(EntityTypeBuilder<OrderLineItem> builder)
    {
        builder.ToTable("order_line_items");
        builder.HasKey(li => li.Id);
        builder.Property(li => li.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(li => li.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(li => li.ShopifyGid).HasColumnName("shopify_gid").IsRequired();
        builder.Property(li => li.NumericId).HasColumnName("numeric_id").IsRequired();
        builder.Property(li => li.Title).HasColumnName("title").IsRequired();
        builder.Property(li => li.VariantTitle).HasColumnName("variant_title").IsRequired();
        builder.Property(li => li.Quantity).HasColumnName("quantity").IsRequired();
        builder.Property(li => li.Price).HasColumnName("price").HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(li => li.Sku).HasColumnName("sku").IsRequired();
        builder.Property(li => li.ProductGid).HasColumnName("product_gid").IsRequired();
        builder.Property(li => li.VariantGid).HasColumnName("variant_gid").IsRequired();
    }
}
