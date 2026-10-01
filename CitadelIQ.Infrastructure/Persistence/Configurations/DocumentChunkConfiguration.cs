using CitadelIQ.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pgvector;

namespace CitadelIQ.Infrastructure.Persistence.Configurations;

public class DocumentChunkConfiguration(int embeddingDimension) : IEntityTypeConfiguration<DocumentChunk>
{
    /// <summary>Shadow property names: the embedding lives on the chunk's row (no join at query time)
    /// but is not part of the <see cref="DocumentChunk"/> domain entity.</summary>
    public const string EmbeddingProperty = "Embedding";
    public const string ModelNameProperty = "ModelName";

    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        builder.ToTable("DocumentChunks");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Text).IsRequired();

        builder.HasOne<Document>()
            .WithMany()
            .HasForeignKey(c => c.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.DocumentId, c.ChunkIndex });

        // pgvector column: fixed-dimension vector, nullable because chunks are inserted before
        // their embeddings are attached (the document only turns Ready after both).
        builder.Property<Vector?>(EmbeddingProperty).HasColumnType($"vector({embeddingDimension})");
        builder.Property<string?>(ModelNameProperty).HasMaxLength(128);
    }
}
