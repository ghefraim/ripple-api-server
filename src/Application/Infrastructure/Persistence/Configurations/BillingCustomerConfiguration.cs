using Application.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Application.Infrastructure.Persistence.Configurations;

public class BillingCustomerConfiguration : IEntityTypeConfiguration<BillingCustomer>
{
    public void Configure(EntityTypeBuilder<BillingCustomer> builder)
    {
        builder.Property(bc => bc.StripeCustomerId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(bc => bc.Email)
            .HasMaxLength(256);

        builder.Property(bc => bc.Name)
            .HasMaxLength(200);

        builder.HasIndex(bc => new { bc.EntityType, bc.EntityId })
            .IsUnique();

        builder.HasIndex(bc => bc.StripeCustomerId)
            .IsUnique();
    }
}
