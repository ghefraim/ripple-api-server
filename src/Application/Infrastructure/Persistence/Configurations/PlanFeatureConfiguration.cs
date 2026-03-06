using Application.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Application.Infrastructure.Persistence.Configurations;

public class PlanFeatureConfiguration : IEntityTypeConfiguration<PlanFeature>
{
    public void Configure(EntityTypeBuilder<PlanFeature> builder)
    {
        builder.Property(pf => pf.FeatureKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(pf => pf.FeatureType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(pf => pf.Value)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasOne(pf => pf.Plan)
            .WithMany(p => p.Features)
            .HasForeignKey(pf => pf.PlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(pf => new { pf.PlanId, pf.FeatureKey })
            .IsUnique();
    }
}
