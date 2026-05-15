using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopifyIntegration.Domain.Entities;

namespace ShopifyIntegration.Infrastructure.Data.Configurations;

public sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("locations");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(l => l.LocationGid).HasColumnName("location_gid").IsRequired();
        builder.Property(l => l.NumericId).HasColumnName("numeric_id").IsRequired();
        builder.Property(l => l.Name).HasColumnName("name").IsRequired();
        builder.Property(l => l.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(l => l.LocationGid).IsUnique();
    }
}
