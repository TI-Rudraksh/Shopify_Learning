using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopifyIntegration.Domain.Entities;

namespace ShopifyIntegration.Infrastructure.Data.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(c => c.ShopifyGid).HasColumnName("shopify_gid").IsRequired();
        builder.Property(c => c.NumericId).HasColumnName("numeric_id").IsRequired();
        builder.Property(c => c.Email).HasColumnName("email").IsRequired();
        builder.Property(c => c.FirstName).HasColumnName("first_name").IsRequired();
        builder.Property(c => c.LastName).HasColumnName("last_name").IsRequired();
        builder.Property(c => c.Phone).HasColumnName("phone");
        builder.Property(c => c.AcceptsMarketing).HasColumnName("accepts_marketing").IsRequired();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(c => c.ShopifyGid).IsUnique();
    }
}
