using Application.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Application.Infrastructure.Persistence.Configurations;

public class EntitlementConfiguration : IEntityTypeConfiguration<Entitlement>
{
    public void Configure(EntityTypeBuilder<Entitlement> builder)
    {
        builder.Property(e => e.FeatureKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.FeatureType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Value)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.GrantedBy)
            .HasMaxLength(100);

        builder.Property(e => e.Reason)
            .HasMaxLength(500);

        builder.HasOne(e => e.Organization)
            .WithMany()
            .HasForeignKey(e => e.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.OrganizationId, e.FeatureKey, e.Source });

        builder.HasIndex(e => e.ExpiresAt);
    }
}
