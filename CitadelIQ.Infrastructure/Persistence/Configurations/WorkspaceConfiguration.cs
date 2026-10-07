using CitadelIQ.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CitadelIQ.Infrastructure.Persistence.Configurations;

public class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.ToTable("Workspaces");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();
        builder.Property(w => w.Kind).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(w => w.Name).IsRequired().HasMaxLength(Workspace.MaxNameLength);
        builder.Property(w => w.JoinCode).HasMaxLength(12);
        // CK_Workspaces_JoinCode and the partial unique index on JoinCode live in the migration.
    }
}
