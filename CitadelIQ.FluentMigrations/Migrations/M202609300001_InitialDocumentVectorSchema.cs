using FluentMigrator;

namespace CitadelIQ.FluentMigrations.Migrations;

[Migration(202609300001, "Folders, Documents, DocumentChunks with pgvector embeddings")]
public class M202609300001_InitialDocumentVectorSchema : Migration
{
    public override void Up()
    {
        Execute.Sql("CREATE EXTENSION IF NOT EXISTS vector;");

        Create.Table("Folders")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey("PK_Folders")
            .WithColumn("Name").AsString(255).NotNullable()
            .WithColumn("ParentFolderId").AsGuid().Nullable()
            .WithColumn("CreatedAtUtc").AsDateTimeOffset().NotNullable();

        Create.ForeignKey("FK_Folders_Folders_ParentFolderId")
            .FromTable("Folders").ForeignColumn("ParentFolderId")
            .ToTable("Folders").PrimaryColumn("Id")
            .OnDelete(System.Data.Rule.Cascade);

        Create.Index("IX_Folders_ParentFolderId").OnTable("Folders").OnColumn("ParentFolderId");

        // Sibling-name uniqueness, case-insensitive (the root's NULL parent never collides).
        Execute.Sql("""CREATE UNIQUE INDEX "UX_Folders_ParentFolderId_LowerName" ON "Folders" ("ParentFolderId", lower("Name"));""");

        Create.Table("Documents")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey("PK_Documents")
            .WithColumn("FolderId").AsGuid().NotNullable()
            .WithColumn("FileName").AsString(512).NotNullable()
            .WithColumn("ContentType").AsString(255).NotNullable()
            .WithColumn("SizeBytes").AsInt64().NotNullable()
            .WithColumn("UploadedAtUtc").AsDateTimeOffset().NotNullable()
            .WithColumn("ProcessingStatus").AsString(32).NotNullable()
            .WithColumn("FailureReason").AsString(1024).Nullable();

        Create.ForeignKey("FK_Documents_Folders_FolderId")
            .FromTable("Documents").ForeignColumn("FolderId")
            .ToTable("Folders").PrimaryColumn("Id")
            .OnDelete(System.Data.Rule.Cascade);

        Create.Index("IX_Documents_FolderId").OnTable("Documents").OnColumn("FolderId");

        Create.Table("DocumentChunks")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey("PK_DocumentChunks")
            .WithColumn("DocumentId").AsGuid().NotNullable()
            .WithColumn("ChunkIndex").AsInt32().NotNullable()
            .WithColumn("Text").AsString(int.MaxValue).NotNullable()
            .WithColumn("PageNumber").AsInt32().Nullable()
            .WithColumn("ModelName").AsString(128).Nullable();

        // FluentMigrator has no pgvector type, so the embedding column is added with raw SQL.
        // Nullable: chunks are inserted before their embeddings are attached.
        Execute.Sql("""ALTER TABLE "DocumentChunks" ADD COLUMN "Embedding" vector(1536);""");

        Create.ForeignKey("FK_DocumentChunks_Documents_DocumentId")
            .FromTable("DocumentChunks").ForeignColumn("DocumentId")
            .ToTable("Documents").PrimaryColumn("Id")
            .OnDelete(System.Data.Rule.Cascade);

        Create.Index("IX_DocumentChunks_DocumentId_ChunkIndex")
            .OnTable("DocumentChunks")
            .OnColumn("DocumentId").Ascending()
            .OnColumn("ChunkIndex").Ascending();

        // Approximate-nearest-neighbour index for cosine distance (the <=> operator). HNSW caps at 2000 dims.
        Execute.Sql("""CREATE INDEX "IX_DocumentChunks_Embedding" ON "DocumentChunks" USING hnsw ("Embedding" vector_cosine_ops);""");

        // The single well-known root ("Home") folder — Folder.RootId is Guid.Empty.
        Insert.IntoTable("Folders").Row(new
        {
            Id = Guid.Empty,
            Name = "Home",
            CreatedAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        });
    }

    public override void Down()
    {
        Delete.Table("DocumentChunks");
        Delete.Table("Documents");
        Delete.Table("Folders");
    }
}
