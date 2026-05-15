using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopifyIntegration.Domain.Entities;

namespace ShopifyIntegration.Infrastructure.Data.Configurations;

public sealed class WebhookEventConfiguration : IEntityTypeConfiguration<WebhookEvent>
{
    public void Configure(EntityTypeBuilder<WebhookEvent> builder)
    {
        builder.ToTable("webhook_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(e => e.Topic).HasColumnName("topic").IsRequired();
        builder.Property(e => e.ShopifyNumericId).HasColumnName("shopify_numeric_id");
        builder.Property(e => e.RawPayload).HasColumnName("raw_payload").IsRequired();
        builder.Property(e => e.ProcessedAt).HasColumnName("processed_at").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").IsRequired();
        builder.Property(e => e.ErrorMessage).HasColumnName("error_message");
    }
}
