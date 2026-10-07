using CitadelIQ.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CitadelIQ.Infrastructure.Persistence.Configurations;

public class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.Property(u => u.Issuer).IsRequired().HasMaxLength(512);
        builder.Property(u => u.Subject).IsRequired().HasMaxLength(255);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(UserAccount.MaxEmailLength);
        builder.Property(u => u.DisplayName).IsRequired().HasMaxLength(UserAccount.MaxDisplayNameLength);
        builder.HasIndex(u => new { u.Issuer, u.Subject }).IsUnique();
    }
}
