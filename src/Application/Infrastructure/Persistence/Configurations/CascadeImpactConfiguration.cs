using Application.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Application.Infrastructure.Persistence.Configurations;

public class CascadeImpactConfiguration : IEntityTypeConfiguration<CascadeImpact>
{
    public void Configure(EntityTypeBuilder<CascadeImpact> builder)
    {
        builder.Property(c => c.Details)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.HasOne(c => c.Disruption)
            .WithMany(d => d.CascadeImpacts)
            .HasForeignKey(c => c.DisruptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.AffectedFlight)
            .WithMany(f => f.CascadeImpacts)
            .HasForeignKey(c => c.AffectedFlightId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.DisruptionId);
    }
}
