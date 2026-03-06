using Application.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Application.Infrastructure.Persistence.Configurations;

public class StripeEventLogConfiguration : IEntityTypeConfiguration<StripeEventLog>
{
    public void Configure(EntityTypeBuilder<StripeEventLog> builder)
    {
        builder.Property(sel => sel.StripeEventId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(sel => sel.EventType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(sel => sel.ErrorMessage)
            .HasMaxLength(2000);

        builder.HasIndex(sel => sel.StripeEventId)
            .IsUnique();

        builder.HasIndex(sel => sel.EventType);

        builder.HasIndex(sel => sel.Status);

        builder.HasIndex(sel => sel.ReceivedAt);
    }
}
