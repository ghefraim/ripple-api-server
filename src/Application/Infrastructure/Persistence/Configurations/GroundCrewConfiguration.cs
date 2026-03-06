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
    }
}
