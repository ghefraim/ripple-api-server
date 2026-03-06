using Application.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Application.Infrastructure.Persistence.Configurations;

public class OperationalRuleConfiguration : IEntityTypeConfiguration<OperationalRule>
{
    public void Configure(EntityTypeBuilder<OperationalRule> builder)
    {
        builder.Property(r => r.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(r => r.Description)
            .HasMaxLength(500);

        builder.Property(r => r.RuleJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.HasOne(r => r.Airport)
            .WithMany()
            .HasForeignKey(r => r.AirportId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.CreatedByUser)
            .WithMany()
            .HasForeignKey(r => r.CreatedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
