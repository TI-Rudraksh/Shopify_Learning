using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopifyIntegration.Domain.Entities;

namespace ShopifyIntegration.Infrastructure.Data.Configurations;

public sealed class OrderNoteAttributeConfiguration : IEntityTypeConfiguration<OrderNoteAttribute>
{
    public void Configure(EntityTypeBuilder<OrderNoteAttribute> builder)
    {
        builder.ToTable("order_note_attributes");
        builder.HasKey(na => na.Id);
        builder.Property(na => na.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(na => na.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(na => na.Name).HasColumnName("name").IsRequired();
        builder.Property(na => na.Value).HasColumnName("value").IsRequired();
        builder.HasOne(na => na.Order)
               .WithMany(o => o.NoteAttributes)
               .HasForeignKey(na => na.OrderId)
               .HasConstraintName("fk_order_note_attributes_orders");
    }
}
