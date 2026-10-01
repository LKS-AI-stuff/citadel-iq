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

        // Self-referencing tree; deleting a folder cascades to its whole subtree.
        builder.HasOne<Folder>()
            .WithMany()
            .HasForeignKey(f => f.ParentFolderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Schema (incl. the unique (ParentFolderId, lower(Name)) index and the seeded root folder)
        // is created by CitadelIQ.FluentMigrations.
    }
}
