using Application.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Application.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.Property(n => n.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(n => n.Message)
            .HasMaxLength(2000)
            .IsRequired();

        builder.HasOne(n => n.Disruption)
            .WithMany()
            .HasForeignKey(n => n.DisruptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(n => n.ActionPlan)
            .WithMany(a => a.Notifications)
            .HasForeignKey(n => n.ActionPlanId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(n => n.Recipient)
            .WithMany()
            .HasForeignKey(n => n.RecipientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(n => new { n.RecipientId, n.SentAt });
    }
}
