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

        builder.Property(r => r.RuleDefinition)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.HasOne(r => r.CreatedByUser)
            .WithMany()
            .HasForeignKey(r => r.CreatedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
