using Application.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Application.Infrastructure.Persistence.Configurations;

public class DisruptionConfiguration : IEntityTypeConfiguration<Disruption>
{
    public void Configure(EntityTypeBuilder<Disruption> builder)
    {
        builder.Property(d => d.Reason)
            .HasMaxLength(500);

        builder.HasOne(d => d.Flight)
            .WithMany(f => f.Disruptions)
            .HasForeignKey(d => d.FlightId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.ReportedBy)
            .WithMany()
            .HasForeignKey(d => d.ReportedById)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(d => d.FlightId);
    }
}
