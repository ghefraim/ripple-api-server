using Application.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Application.Infrastructure.Persistence.Configurations;

public class GroundCrewConfiguration : IEntityTypeConfiguration<GroundCrew>
{
    public void Configure(EntityTypeBuilder<GroundCrew> builder)
    {
        builder.Property(c => c.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasOne(c => c.Airport)
            .WithMany(a => a.GroundCrews)
            .HasForeignKey(c => c.AirportId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
