using Application.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Application.Infrastructure.Persistence.Configurations;

public class CrewContactConfiguration : IEntityTypeConfiguration<CrewContact>
{
    public void Configure(EntityTypeBuilder<CrewContact> builder)
    {
        builder.Property(c => c.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.PhoneNumber)
            .HasMaxLength(30)
            .IsRequired();

        builder.HasOne(c => c.Crew)
            .WithMany(g => g.Contacts)
            .HasForeignKey(c => c.CrewId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.CrewId);
        builder.HasIndex(c => c.TelegramChatId);
    }
}
