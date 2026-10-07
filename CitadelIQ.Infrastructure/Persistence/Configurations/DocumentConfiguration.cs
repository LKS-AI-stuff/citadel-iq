using CitadelIQ.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CitadelIQ.Infrastructure.Persistence.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.FileName).IsRequired().HasMaxLength(512);
        builder.Property(d => d.ContentType).IsRequired().HasMaxLength(255);

        // Stored as readable text ("Ready", "Failed", ...) rather than an int.
        builder.Property(d => d.Status)
            .HasColumnName("ProcessingStatus")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(d => d.FailureReason).HasMaxLength(1024);

        // In the database: composite (WorkspaceId, FolderId) → Folders (WorkspaceId, Id).
        builder.HasOne<Folder>()
            .WithMany()
            .HasForeignKey(d => d.FolderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<UserAccount>().WithMany().HasForeignKey(d => d.UploadedByUserId);

        builder.HasIndex(d => d.FolderId);
    }
}
