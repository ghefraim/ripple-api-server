using Application.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Application.Infrastructure.Persistence.Configurations;

public class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.Property(p => p.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasMaxLength(500);

        builder.Property(p => p.StripeProductId)
            .HasMaxLength(100);

        builder.Property(p => p.StripeMonthlyPriceId)
            .HasMaxLength(100);

        builder.Property(p => p.StripeAnnualPriceId)
            .HasMaxLength(100);

        builder.Property(p => p.MonthlyPrice)
            .HasPrecision(18, 2);

        builder.Property(p => p.AnnualPrice)
            .HasPrecision(18, 2);

        builder.HasIndex(p => p.Name)
            .IsUnique();

        builder.HasIndex(p => p.StripeProductId);
    }
}
