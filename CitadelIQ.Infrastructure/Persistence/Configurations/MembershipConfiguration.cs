using CitadelIQ.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CitadelIQ.Infrastructure.Persistence.Configurations;

public class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("Memberships");
        // UserId is the key: one workspace per user.
        builder.HasKey(m => m.UserId);
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(16).IsRequired();

        builder.HasOne<UserAccount>().WithOne().HasForeignKey<Membership>(m => m.UserId);
        builder.HasOne<Workspace>().WithMany().HasForeignKey(m => m.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(m => m.WorkspaceId);
    }
}
