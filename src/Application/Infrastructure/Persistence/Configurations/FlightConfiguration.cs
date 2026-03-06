using Application.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Application.Infrastructure.Persistence.Configurations;

public class FlightConfiguration : IEntityTypeConfiguration<Flight>
{
    public void Configure(EntityTypeBuilder<Flight> builder)
    {
        builder.Property(f => f.FlightNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(f => f.Airline)
            .HasMaxLength(100);

        builder.Property(f => f.Origin)
            .HasMaxLength(10);

        builder.Property(f => f.Destination)
            .HasMaxLength(10);

        builder.HasOne(f => f.Gate)
            .WithMany(g => g.Flights)
            .HasForeignKey(f => f.GateId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(f => f.Crew)
            .WithMany(c => c.AssignedFlights)
            .HasForeignKey(f => f.CrewId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(f => f.TurnaroundPair)
            .WithOne()
            .HasForeignKey<Flight>(f => f.TurnaroundPairId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(f => new { f.OrganizationId, f.ScheduledTime });
        builder.HasIndex(f => f.GateId);
    }
}
