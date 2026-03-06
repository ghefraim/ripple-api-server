using Application.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Application.Infrastructure.Persistence.Configurations;

public class AirportConfigConfiguration : IEntityTypeConfiguration<AirportConfig>
{
    public void Configure(EntityTypeBuilder<AirportConfig> builder)
    {
        builder.Property(a => a.IataCode)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(a => a.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(a => a.Timezone)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(a => a.OrganizationId)
            .IsUnique();
    }
}
