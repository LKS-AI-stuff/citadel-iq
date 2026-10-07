using CitadelIQ.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CitadelIQ.Infrastructure.Persistence.Configurations;

public class FolderConfiguration : IEntityTypeConfiguration<Folder>
{
    public void Configure(EntityTypeBuilder<Folder> builder)
    {
        builder.ToTable("Folders");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();
        builder.Property(f => f.Name).IsRequired().HasMaxLength(255);

        builder.HasOne<Workspace>().WithMany().HasForeignKey(f => f.WorkspaceId).OnDelete(DeleteBehavior.Cascade);

        // Self-referencing tree; deleting a folder cascades to its whole subtree. In the database this is the
        // composite (WorkspaceId, ParentFolderId) → (WorkspaceId, Id) key, so a parent is always in the same workspace.
        builder.HasOne<Folder>()
            .WithMany()
            .HasForeignKey(f => f.ParentFolderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Schema (incl. the unique (ParentFolderId, lower(Name)) index, one-root-per-workspace index and the
        // row-level security policy) is created by CitadelIQ.FluentMigrations.
    }
}
