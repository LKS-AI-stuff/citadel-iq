using FluentMigrator;

namespace CitadelIQ.FluentMigrations.Migrations;

/// <summary>
/// The whole multi-tenant schema: identity tables (workspaces, users, memberships, join requests) and the workspace-
/// owned content tables (folders, documents, chunks with pgvector embeddings), protected by row-level security.
/// Rewritten in place when workspaces were introduced (development data only): existing databases must be reset.
/// Runs as the database owner; the API itself connects as the restricted <see cref="AppRole"/>.
/// </summary>
[Migration(202609300001, "Workspaces, users, folders, documents, chunks with pgvector embeddings, row-level security")]
public class M202609300001_InitialDocumentVectorSchema : Migration
{
    /// <summary>The runtime login: DML on the application tables only, no DDL, never BYPASSRLS. Created NOLOGIN here if
    /// missing; the operator (or the compose init script) gives it LOGIN and a password.</summary>
    public const string AppRole = "citadeliq_app";

    /// <summary>Workspace-owned tables. Each has a WorkspaceId, composite same-workspace foreign keys and an RLS policy.</summary>
    public static readonly string[] WorkspaceTables = ["Folders", "Documents", "DocumentChunks"];

    public override void Up()
    {
        Execute.Sql("CREATE EXTENSION IF NOT EXISTS vector;");

        // ---- Identity tables (not under RLS: they are read before a workspace is known) ----

        Create.Table("Workspaces")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey("PK_Workspaces")
            .WithColumn("Kind").AsString(16).NotNullable()
            .WithColumn("Name").AsString(100).NotNullable()
            .WithColumn("JoinCode").AsString(12).Nullable()
            .WithColumn("CreatedAtUtc").AsDateTimeOffset().NotNullable();

        // Organizations have a join code; individual workspaces never do.
        Execute.Sql("""ALTER TABLE "Workspaces" ADD CONSTRAINT "CK_Workspaces_JoinCode" CHECK (("Kind" = 'Organization') = ("JoinCode" IS NOT NULL));""");
        Execute.Sql("""CREATE UNIQUE INDEX "UX_Workspaces_JoinCode" ON "Workspaces" ("JoinCode") WHERE "JoinCode" IS NOT NULL;""");

        Create.Table("Users")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey("PK_Users")
            .WithColumn("Issuer").AsString(512).NotNullable()
            .WithColumn("Subject").AsString(255).NotNullable()
            .WithColumn("Email").AsString(320).NotNullable()
            .WithColumn("DisplayName").AsString(200).NotNullable()
            .WithColumn("CreatedAtUtc").AsDateTimeOffset().NotNullable()
            .WithColumn("LastSignInAtUtc").AsDateTimeOffset().NotNullable()
            .WithColumn("ClosedAtUtc").AsDateTimeOffset().Nullable();

        // Users are identified by the identity provider's (issuer, subject) — never by email.
        Create.Index("UX_Users_Issuer_Subject").OnTable("Users")
            .OnColumn("Issuer").Ascending()
            .OnColumn("Subject").Ascending()
            .WithOptions().Unique();

        // UserId is the primary key: one workspace per user, enforced by the schema.
        Create.Table("Memberships")
            .WithColumn("UserId").AsGuid().NotNullable().PrimaryKey("PK_Memberships")
            .WithColumn("WorkspaceId").AsGuid().NotNullable()
            .WithColumn("Role").AsString(16).NotNullable()
            .WithColumn("JoinedAtUtc").AsDateTimeOffset().NotNullable();

        Create.ForeignKey("FK_Memberships_Users_UserId")
            .FromTable("Memberships").ForeignColumn("UserId")
            .ToTable("Users").PrimaryColumn("Id");
        Create.ForeignKey("FK_Memberships_Workspaces_WorkspaceId")
            .FromTable("Memberships").ForeignColumn("WorkspaceId")
            .ToTable("Workspaces").PrimaryColumn("Id")
            .OnDelete(System.Data.Rule.Cascade);
        Create.Index("IX_Memberships_WorkspaceId").OnTable("Memberships").OnColumn("WorkspaceId");

        Create.Table("JoinRequests")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey("PK_JoinRequests")
            .WithColumn("UserId").AsGuid().NotNullable()
            .WithColumn("WorkspaceId").AsGuid().NotNullable()
            .WithColumn("Status").AsString(16).NotNullable()
            .WithColumn("CreatedAtUtc").AsDateTimeOffset().NotNullable()
            .WithColumn("DecidedAtUtc").AsDateTimeOffset().Nullable()
            .WithColumn("DecidedByUserId").AsGuid().Nullable();

        Create.ForeignKey("FK_JoinRequests_Users_UserId")
            .FromTable("JoinRequests").ForeignColumn("UserId")
            .ToTable("Users").PrimaryColumn("Id");
        Create.ForeignKey("FK_JoinRequests_Users_DecidedByUserId")
            .FromTable("JoinRequests").ForeignColumn("DecidedByUserId")
            .ToTable("Users").PrimaryColumn("Id");
        Create.ForeignKey("FK_JoinRequests_Workspaces_WorkspaceId")
            .FromTable("JoinRequests").ForeignColumn("WorkspaceId")
            .ToTable("Workspaces").PrimaryColumn("Id")
            .OnDelete(System.Data.Rule.Cascade);
        Execute.Sql("""CREATE UNIQUE INDEX "UX_JoinRequests_PendingPerUser" ON "JoinRequests" ("UserId") WHERE "Status" = 'Pending';""");
        Create.Index("IX_JoinRequests_WorkspaceId_Status").OnTable("JoinRequests")
            .OnColumn("WorkspaceId").Ascending()
            .OnColumn("Status").Ascending();

        // ---- Workspace-owned content tables ----

        Create.Table("Folders")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey("PK_Folders")
            .WithColumn("WorkspaceId").AsGuid().NotNullable()
            .WithColumn("Name").AsString(255).NotNullable()
            .WithColumn("ParentFolderId").AsGuid().Nullable()
            .WithColumn("CreatedAtUtc").AsDateTimeOffset().NotNullable();

        Create.ForeignKey("FK_Folders_Workspaces_WorkspaceId")
            .FromTable("Folders").ForeignColumn("WorkspaceId")
            .ToTable("Workspaces").PrimaryColumn("Id")
            .OnDelete(System.Data.Rule.Cascade);
        Execute.Sql("""ALTER TABLE "Folders" ADD CONSTRAINT "UQ_Folders_WorkspaceId_Id" UNIQUE ("WorkspaceId", "Id");""");

        // Composite FK: a folder's parent must be in the same workspace. MATCH SIMPLE leaves the root's NULL parent
        // unchecked. Deleting a folder cascades to its whole subtree.
        Execute.Sql("""
            ALTER TABLE "Folders" ADD CONSTRAINT "FK_Folders_Parent_SameWorkspace"
              FOREIGN KEY ("WorkspaceId", "ParentFolderId") REFERENCES "Folders" ("WorkspaceId", "Id") ON DELETE CASCADE;
            """);
        Create.Index("IX_Folders_ParentFolderId").OnTable("Folders").OnColumn("ParentFolderId");

        // Sibling-name uniqueness, case-insensitive (parent ids are workspace-specific, so this is per workspace).
        Execute.Sql("""CREATE UNIQUE INDEX "UX_Folders_ParentFolderId_LowerName" ON "Folders" ("ParentFolderId", lower("Name"));""");

        // Exactly one root ("Home") folder per workspace.
        Execute.Sql("""CREATE UNIQUE INDEX "UX_Folders_WorkspaceRoot" ON "Folders" ("WorkspaceId") WHERE "ParentFolderId" IS NULL;""");

        Create.Table("Documents")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey("PK_Documents")
            .WithColumn("WorkspaceId").AsGuid().NotNullable()
            .WithColumn("FolderId").AsGuid().NotNullable()
            .WithColumn("FileName").AsString(512).NotNullable()
            .WithColumn("ContentType").AsString(255).NotNullable()
            .WithColumn("SizeBytes").AsInt64().NotNullable()
            .WithColumn("UploadedAtUtc").AsDateTimeOffset().NotNullable()
            .WithColumn("ProcessingStatus").AsString(32).NotNullable()
            .WithColumn("FailureReason").AsString(1024).Nullable()
            .WithColumn("UploadedByUserId").AsGuid().Nullable();

        Create.ForeignKey("FK_Documents_Users_UploadedByUserId")
            .FromTable("Documents").ForeignColumn("UploadedByUserId")
            .ToTable("Users").PrimaryColumn("Id");
        Execute.Sql("""ALTER TABLE "Documents" ADD CONSTRAINT "UQ_Documents_WorkspaceId_Id" UNIQUE ("WorkspaceId", "Id");""");
        Execute.Sql("""
            ALTER TABLE "Documents" ADD CONSTRAINT "FK_Documents_Folder_SameWorkspace"
              FOREIGN KEY ("WorkspaceId", "FolderId") REFERENCES "Folders" ("WorkspaceId", "Id") ON DELETE CASCADE;
            """);
        Create.Index("IX_Documents_FolderId").OnTable("Documents").OnColumn("FolderId");

        Create.Table("DocumentChunks")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey("PK_DocumentChunks")
            .WithColumn("WorkspaceId").AsGuid().NotNullable()
            .WithColumn("DocumentId").AsGuid().NotNullable()
            .WithColumn("ChunkIndex").AsInt32().NotNullable()
            .WithColumn("Text").AsString(int.MaxValue).NotNullable()
            .WithColumn("PageNumber").AsInt32().Nullable()
            .WithColumn("ModelName").AsString(128).Nullable();

        // FluentMigrator has no pgvector type, so the embedding column is added with raw SQL.
        // Nullable: chunks are inserted before their embeddings are attached.
        Execute.Sql("""ALTER TABLE "DocumentChunks" ADD COLUMN "Embedding" vector(1536);""");

        Execute.Sql("""
            ALTER TABLE "DocumentChunks" ADD CONSTRAINT "FK_DocumentChunks_Document_SameWorkspace"
              FOREIGN KEY ("WorkspaceId", "DocumentId") REFERENCES "Documents" ("WorkspaceId", "Id") ON DELETE CASCADE;
            """);

        Create.Index("IX_DocumentChunks_DocumentId_ChunkIndex")
            .OnTable("DocumentChunks")
            .OnColumn("DocumentId").Ascending()
            .OnColumn("ChunkIndex").Ascending();

        // Lets the planner pick an exact scan for small workspaces instead of a post-filtered HNSW scan.
        Create.Index("IX_DocumentChunks_WorkspaceId").OnTable("DocumentChunks").OnColumn("WorkspaceId");

        // Approximate-nearest-neighbour index for cosine distance (the <=> operator). HNSW caps at 2000 dims.
        Execute.Sql("""CREATE INDEX "IX_DocumentChunks_Embedding" ON "DocumentChunks" USING hnsw ("Embedding" vector_cosine_ops);""");

        // ---- Row-level security: each connection is stamped with app.workspace_id by the API; unset/empty ⇒ NULL ⇒
        // no rows visible and no rows writable (fail closed). FORCE also applies it to the table owner. ----

        foreach (var table in WorkspaceTables)
        {
            Execute.Sql($"""
                ALTER TABLE "{table}" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "{table}" FORCE ROW LEVEL SECURITY;
                CREATE POLICY "{table}_workspace_isolation" ON "{table}"
                  USING      ("WorkspaceId" = nullif(current_setting('app.workspace_id', true), '')::uuid)
                  WITH CHECK ("WorkspaceId" = nullif(current_setting('app.workspace_id', true), '')::uuid);
                """);
        }

        // ---- Runtime role. Superusers and BYPASSRLS roles ignore RLS, so the API must not connect as the owner. ----

        Execute.Sql($"""
            DO $$
            BEGIN
              IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{AppRole}') THEN
                CREATE ROLE {AppRole} NOLOGIN NOSUPERUSER NOBYPASSRLS;
              END IF;
            END
            $$;
            GRANT USAGE ON SCHEMA public TO {AppRole};
            GRANT SELECT, INSERT, UPDATE, DELETE ON "Workspaces", "Users", "Memberships", "JoinRequests",
              "Folders", "Documents", "DocumentChunks" TO {AppRole};
            """);
    }

    public override void Down()
    {
        Delete.Table("DocumentChunks");
        Delete.Table("Documents");
        Delete.Table("Folders");
        Delete.Table("JoinRequests");
        Delete.Table("Memberships");
        Delete.Table("Users");
        Delete.Table("Workspaces");
    }
}
