using Application.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Application.Infrastructure.Persistence.Configurations;

public class GateConfiguration : IEntityTypeConfiguration<Gate>
{
    public void Configure(EntityTypeBuilder<Gate> builder)
    {
        builder.Property(g => g.Code)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasOne(g => g.Airport)
            .WithMany(a => a.Gates)
            .HasForeignKey(g => g.AirportId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(g => new { g.OrganizationId, g.Code })
            .IsUnique();
    }
}
