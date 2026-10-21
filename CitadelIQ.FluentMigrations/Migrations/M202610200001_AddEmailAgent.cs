using FluentMigrator;

namespace CitadelIQ.FluentMigrations.Migrations;

/// <summary>
/// The email questions agent. Three system tables (no row-level security, no email content: they let the background
/// workers find work across workspaces) and four workspace tables under row-level security (everything that holds a
/// question, answer, draft or outbound body). Plus the draft-notification opt-out on memberships and a case-insensitive
/// lookup index on user emails (deliberately not unique).
/// </summary>
[Migration(202610200001, "Email agent: processing/outbox system tables, email threads, questions, drafts, outbound emails")]
public class M202610200001_AddEmailAgent : Migration
{
    public static readonly string[] WorkspaceTables = ["EmailThreads", "EmailQuestions", "EmailDrafts", "OutboundEmails"];

    public override void Up()
    {
        // ---- Identity tables ----

        Alter.Table("Memberships").AddColumn("EmailDraftNotifications").AsBoolean().NotNullable().WithDefaultValue(true);
        Execute.Sql("""CREATE INDEX "IX_Users_LowerEmail" ON "Users" (lower("Email"));""");

        // ---- System tables: ids and state only, never subjects, bodies or full addresses ----

        // Processing state and the operator-only log in one table (requirements decision 17, revision 6).
        Execute.Sql("""
            CREATE TABLE "InboundEmails" (
              "Id"               uuid         NOT NULL CONSTRAINT "PK_InboundEmails" PRIMARY KEY,
              "UidValidity"      bigint       NOT NULL,
              "Uid"              bigint       NOT NULL,
              "ReceivedAtUtc"    timestamptz  NOT NULL,
              "Status"           varchar(16)  NOT NULL,
              "IgnoreReason"     varchar(32)  NULL,
              "SenderDomain"     varchar(255) NULL,
              "WorkspaceId"      uuid         NULL CONSTRAINT "FK_InboundEmails_Workspaces_WorkspaceId"
                                              REFERENCES "Workspaces" ("Id") ON DELETE SET NULL,
              "MessageIdHash"    char(64)     NULL,
              "Attempts"         integer      NOT NULL DEFAULT 0,
              "NextAttemptAtUtc" timestamptz  NOT NULL,
              "LockedUntilUtc"   timestamptz  NULL,
              "LastErrorType"    varchar(100) NULL,
              "UpdatedAtUtc"     timestamptz  NOT NULL,
              CONSTRAINT "UX_InboundEmails_Key" UNIQUE ("UidValidity", "Uid")
            );
            CREATE INDEX "IX_InboundEmails_Status_NextAttemptAtUtc" ON "InboundEmails" ("Status", "NextAttemptAtUtc");
            CREATE INDEX "IX_InboundEmails_MessageIdHash" ON "InboundEmails" ("MessageIdHash") WHERE "MessageIdHash" IS NOT NULL;
            CREATE INDEX "IX_InboundEmails_ReceivedAtUtc" ON "InboundEmails" ("ReceivedAtUtc");
            """);

        Execute.Sql("""
            CREATE TABLE "OutboundEmailQueue" (
              "OutboundEmailId"  uuid        NOT NULL CONSTRAINT "PK_OutboundEmailQueue" PRIMARY KEY,
              "WorkspaceId"      uuid        NOT NULL CONSTRAINT "FK_OutboundEmailQueue_Workspaces_WorkspaceId"
                                             REFERENCES "Workspaces" ("Id") ON DELETE CASCADE,
              "Status"           varchar(16) NOT NULL,
              "Attempts"         integer     NOT NULL DEFAULT 0,
              "NextAttemptAtUtc" timestamptz NOT NULL,
              "LockedUntilUtc"   timestamptz NULL,
              "SentAtUtc"        timestamptz NULL,
              "CreatedAtUtc"     timestamptz NOT NULL
            );
            CREATE INDEX "IX_OutboundEmailQueue_Status_NextAttemptAtUtc" ON "OutboundEmailQueue" ("Status", "NextAttemptAtUtc");
            CREATE INDEX "IX_OutboundEmailQueue_SentAtUtc" ON "OutboundEmailQueue" ("SentAtUtc") WHERE "SentAtUtc" IS NOT NULL;
            """);

        // At most one pending notification per admin (the primary key), due no earlier than LastSentAtUtc + interval.
        Execute.Sql("""
            CREATE TABLE "AdminNotificationSchedule" (
              "UserId"        uuid        NOT NULL CONSTRAINT "PK_AdminNotificationSchedule" PRIMARY KEY
                                          CONSTRAINT "FK_AdminNotificationSchedule_Users_UserId" REFERENCES "Users" ("Id") ON DELETE CASCADE,
              "WorkspaceId"   uuid        NOT NULL CONSTRAINT "FK_AdminNotificationSchedule_Workspaces_WorkspaceId"
                                          REFERENCES "Workspaces" ("Id") ON DELETE CASCADE,
              "DueAtUtc"      timestamptz NULL,
              "LastSentAtUtc" timestamptz NULL
            );
            CREATE INDEX "IX_AdminNotificationSchedule_DueAtUtc" ON "AdminNotificationSchedule" ("DueAtUtc") WHERE "DueAtUtc" IS NOT NULL;
            """);

        // ---- Workspace tables (row-level security, composite same-workspace foreign keys) ----

        Execute.Sql("""
            CREATE TABLE "EmailThreads" (
              "Id"                  uuid         NOT NULL CONSTRAINT "PK_EmailThreads" PRIMARY KEY,
              "WorkspaceId"         uuid         NOT NULL CONSTRAINT "FK_EmailThreads_Workspaces_WorkspaceId"
                                                 REFERENCES "Workspaces" ("Id") ON DELETE CASCADE,
              "UserId"              uuid         NOT NULL CONSTRAINT "FK_EmailThreads_Users_UserId" REFERENCES "Users" ("Id"),
              "Subject"             varchar(300) NOT NULL,
              "AutomaticReplyCount" integer      NOT NULL,
              "AdminReplied"        boolean      NOT NULL,
              "CreatedAtUtc"        timestamptz  NOT NULL,
              "LastActivityAtUtc"   timestamptz  NOT NULL,
              CONSTRAINT "UQ_EmailThreads_WorkspaceId_Id" UNIQUE ("WorkspaceId", "Id")
            );
            CREATE INDEX "IX_EmailThreads_WorkspaceId_LastActivityAtUtc" ON "EmailThreads" ("WorkspaceId", "LastActivityAtUtc");
            """);

        Execute.Sql("""
            CREATE TABLE "EmailQuestions" (
              "Id"                uuid             NOT NULL CONSTRAINT "PK_EmailQuestions" PRIMARY KEY,
              "WorkspaceId"       uuid             NOT NULL CONSTRAINT "FK_EmailQuestions_Workspaces_WorkspaceId"
                                                   REFERENCES "Workspaces" ("Id") ON DELETE CASCADE,
              "ThreadId"          uuid             NULL,
              "InboundEmailId"    uuid             NULL CONSTRAINT "FK_EmailQuestions_InboundEmails_InboundEmailId"
                                                   REFERENCES "InboundEmails" ("Id") ON DELETE SET NULL,
              "SenderUserId"      uuid             NOT NULL CONSTRAINT "FK_EmailQuestions_Users_SenderUserId" REFERENCES "Users" ("Id"),
              "ReceivedAtUtc"     timestamptz      NOT NULL,
              "Subject"           varchar(300)     NOT NULL,
              "QuestionText"      text             NULL,
              "Outcome"           varchar(32)      NOT NULL,
              "Reason"            varchar(32)      NULL,
              "Confidence"        double precision NULL,
              "WouldAutoReply"    boolean          NULL,
              "AnswerText"        text             NULL,
              "Sources"           jsonb            NOT NULL DEFAULT '[]',
              "InboundMessageId"  varchar(998)     NULL,
              "InboundReferences" varchar(4000)    NULL,
              "ExpiresAtUtc"      timestamptz      NOT NULL,
              CONSTRAINT "UQ_EmailQuestions_WorkspaceId_Id" UNIQUE ("WorkspaceId", "Id"),
              CONSTRAINT "FK_EmailQuestions_Thread_SameWorkspace"
                FOREIGN KEY ("WorkspaceId", "ThreadId") REFERENCES "EmailThreads" ("WorkspaceId", "Id") ON DELETE CASCADE
            );
            CREATE INDEX "IX_EmailQuestions_WorkspaceId_ReceivedAtUtc" ON "EmailQuestions" ("WorkspaceId", "ReceivedAtUtc" DESC);
            CREATE INDEX "IX_EmailQuestions_WorkspaceId_SenderUserId_ReceivedAtUtc" ON "EmailQuestions" ("WorkspaceId", "SenderUserId", "ReceivedAtUtc");
            CREATE INDEX "IX_EmailQuestions_ThreadId" ON "EmailQuestions" ("ThreadId");
            CREATE INDEX "IX_EmailQuestions_InboundEmailId" ON "EmailQuestions" ("InboundEmailId");
            """);

        Execute.Sql("""
            CREATE TABLE "EmailDrafts" (
              "Id"              uuid        NOT NULL CONSTRAINT "PK_EmailDrafts" PRIMARY KEY,
              "WorkspaceId"     uuid        NOT NULL CONSTRAINT "FK_EmailDrafts_Workspaces_WorkspaceId"
                                            REFERENCES "Workspaces" ("Id") ON DELETE CASCADE,
              "QuestionId"      uuid        NOT NULL,
              "Status"          varchar(16) NOT NULL,
              "Reason"          varchar(32) NOT NULL,
              "SuggestedText"   text        NULL,
              "Missing"         text        NULL,
              "SentText"        text        NULL,
              "CreatedAtUtc"    timestamptz NOT NULL,
              "ExpiresAtUtc"    timestamptz NOT NULL,
              "DecidedByUserId" uuid        NULL CONSTRAINT "FK_EmailDrafts_Users_DecidedByUserId" REFERENCES "Users" ("Id"),
              "DecidedAtUtc"    timestamptz NULL,
              CONSTRAINT "UX_EmailDrafts_QuestionId" UNIQUE ("QuestionId"),
              CONSTRAINT "FK_EmailDrafts_Question_SameWorkspace"
                FOREIGN KEY ("WorkspaceId", "QuestionId") REFERENCES "EmailQuestions" ("WorkspaceId", "Id") ON DELETE CASCADE
            );
            CREATE INDEX "IX_EmailDrafts_Pending" ON "EmailDrafts" ("WorkspaceId", "CreatedAtUtc") WHERE "Status" = 'Pending';
            """);

        Execute.Sql("""
            CREATE TABLE "OutboundEmails" (
              "Id"           uuid          NOT NULL CONSTRAINT "PK_OutboundEmails" PRIMARY KEY,
              "WorkspaceId"  uuid          NOT NULL CONSTRAINT "FK_OutboundEmails_Workspaces_WorkspaceId"
                                           REFERENCES "Workspaces" ("Id") ON DELETE CASCADE,
              "Kind"         varchar(32)   NOT NULL,
              "ToUserId"     uuid          NOT NULL CONSTRAINT "FK_OutboundEmails_Users_ToUserId" REFERENCES "Users" ("Id"),
              "Subject"      varchar(400)  NOT NULL,
              "Body"         text          NOT NULL,
              "MessageId"    varchar(255)  NOT NULL CONSTRAINT "UX_OutboundEmails_MessageId" UNIQUE,
              "InReplyTo"    varchar(998)  NULL,
              "References"   varchar(4000) NULL,
              "ThreadId"     uuid          NULL,
              "QuestionId"   uuid          NULL,
              "Status"       varchar(16)   NOT NULL,
              "CancelReason" varchar(64)   NULL,
              "CreatedAtUtc" timestamptz   NOT NULL,
              "SentAtUtc"    timestamptz   NULL,
              CONSTRAINT "FK_OutboundEmails_Thread_SameWorkspace"
                FOREIGN KEY ("WorkspaceId", "ThreadId") REFERENCES "EmailThreads" ("WorkspaceId", "Id") ON DELETE CASCADE,
              CONSTRAINT "FK_OutboundEmails_Question_SameWorkspace"
                FOREIGN KEY ("WorkspaceId", "QuestionId") REFERENCES "EmailQuestions" ("WorkspaceId", "Id") ON DELETE CASCADE
            );
            CREATE INDEX "IX_OutboundEmails_WorkspaceId_MessageId" ON "OutboundEmails" ("WorkspaceId", "MessageId");
            CREATE INDEX "IX_OutboundEmails_QuestionId" ON "OutboundEmails" ("QuestionId");
            CREATE INDEX "IX_OutboundEmails_WorkspaceId_CreatedAtUtc" ON "OutboundEmails" ("WorkspaceId", "CreatedAtUtc");
            """);

        // Same policy as the initial migration's content tables: unset/empty app.workspace_id ⇒ no rows (fail closed).
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

        Execute.Sql($"""
            GRANT SELECT, INSERT, UPDATE, DELETE ON "InboundEmails", "OutboundEmailQueue", "AdminNotificationSchedule",
              "EmailThreads", "EmailQuestions", "EmailDrafts", "OutboundEmails" TO {M202609300001_InitialDocumentVectorSchema.AppRole};
            """);
    }

    public override void Down()
    {
        Delete.Table("OutboundEmails");
        Delete.Table("EmailDrafts");
        Delete.Table("EmailQuestions");
        Delete.Table("EmailThreads");
        Delete.Table("AdminNotificationSchedule");
        Delete.Table("OutboundEmailQueue");
        Delete.Table("InboundEmails");
        Execute.Sql("""DROP INDEX "IX_Users_LowerEmail";""");
        Delete.Column("EmailDraftNotifications").FromTable("Memberships");
    }
}
