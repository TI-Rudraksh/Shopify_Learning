using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopifyIntegration.Domain.Entities;

namespace ShopifyIntegration.Infrastructure.Data.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(p => p.ShopifyGid).HasColumnName("shopify_gid").IsRequired();
        builder.Property(p => p.NumericId).HasColumnName("numeric_id").IsRequired();
        builder.Property(p => p.Title).HasColumnName("title").IsRequired();
        builder.Property(p => p.Vendor).HasColumnName("vendor").IsRequired();
        builder.Property(p => p.Status).HasColumnName("status").IsRequired();
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(p => p.ShopifyGid).IsUnique();
    }
}
