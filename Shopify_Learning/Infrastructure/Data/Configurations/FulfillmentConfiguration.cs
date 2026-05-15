using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopifyIntegration.Domain.Entities;

namespace ShopifyIntegration.Infrastructure.Data.Configurations;

public sealed class FulfillmentConfiguration : IEntityTypeConfiguration<Fulfillment>
{
    public void Configure(EntityTypeBuilder<Fulfillment> builder)
    {
        builder.ToTable("fulfillments");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(f => f.ShopifyGid).HasColumnName("shopify_gid").IsRequired();
        builder.Property(f => f.NumericId).HasColumnName("numeric_id").IsRequired();
        builder.Property(f => f.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(f => f.Status).HasColumnName("status").IsRequired();
        builder.Property(f => f.TrackingNumber).HasColumnName("tracking_number");
        builder.Property(f => f.TrackingCompany).HasColumnName("tracking_company");
        builder.Property(f => f.TrackingUrl).HasColumnName("tracking_url");
        builder.Property(f => f.FulfillmentOrderGid).HasColumnName("fulfillment_order_gid");
        builder.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(f => f.ShopifyGid).IsUnique();
    }
}
