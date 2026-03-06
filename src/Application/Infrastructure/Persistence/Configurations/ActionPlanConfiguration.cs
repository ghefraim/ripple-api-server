using Application.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Application.Infrastructure.Persistence.Configurations;

public class ActionPlanConfiguration : IEntityTypeConfiguration<ActionPlan>
{
    public void Configure(EntityTypeBuilder<ActionPlan> builder)
    {
        builder.Property(a => a.LlmRawOutput)
            .HasColumnType("text");

        builder.Property(a => a.Actions)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.HasOne(a => a.Disruption)
            .WithOne(d => d.ActionPlan)
            .HasForeignKey<ActionPlan>(a => a.DisruptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
