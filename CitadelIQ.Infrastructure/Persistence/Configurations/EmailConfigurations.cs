using System.Text.Json;
using CitadelIQ.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CitadelIQ.Infrastructure.Persistence.Configurations;

// The email agent's workspace tables. Schema (composite same-workspace FKs, RLS policies, partial indexes) lives in
// M202610200001_AddEmailAgent; the system tables (InboundEmails, OutboundEmailQueue, AdminNotificationSchedule) are
// not in the EF model at all — their repositories use SQL.

public class EmailThreadConfiguration : IEntityTypeConfiguration<EmailThread>
{
    public void Configure(EntityTypeBuilder<EmailThread> builder)
    {
        builder.ToTable("EmailThreads");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Subject).IsRequired().HasMaxLength(EmailThread.MaxSubjectLength);

        builder.HasOne<Workspace>().WithMany().HasForeignKey(t => t.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(t => t.UserId);
    }
}

public class EmailQuestionConfiguration : IEntityTypeConfiguration<EmailQuestion>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<EmailQuestion> builder)
    {
        builder.ToTable("EmailQuestions");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).ValueGeneratedNever();
        builder.Property(q => q.Subject).IsRequired().HasMaxLength(EmailQuestion.MaxSubjectLength);
        builder.Property(q => q.Outcome).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(q => q.Reason).HasConversion<string>().HasMaxLength(32);
        builder.Property(q => q.InboundMessageId).HasMaxLength(EmailQuestion.MaxMessageIdLength);
        builder.Property(q => q.InboundReferences).HasMaxLength(EmailQuestion.MaxReferencesLength);
        builder.Ignore(q => q.IsDecided);

        builder.Property(q => q.Sources)
            .HasColumnType("jsonb")
            .IsRequired()
            .HasConversion(
                new ValueConverter<IReadOnlyList<EmailSourceRef>, string>(
                    v => JsonSerializer.Serialize(v, Json),
                    s => JsonSerializer.Deserialize<List<EmailSourceRef>>(s, Json) ?? new List<EmailSourceRef>()),
                new ValueComparer<IReadOnlyList<EmailSourceRef>>(
                    (a, b) => (a ?? new List<EmailSourceRef>()).SequenceEqual(b ?? new List<EmailSourceRef>()),
                    v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
                    v => v.ToList()));

        builder.HasOne<Workspace>().WithMany().HasForeignKey(q => q.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(q => q.SenderUserId);
        // In the database: composite (WorkspaceId, ThreadId) → EmailThreads (WorkspaceId, Id).
        builder.HasOne<EmailThread>().WithMany().HasForeignKey(q => q.ThreadId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class EmailDraftConfiguration : IEntityTypeConfiguration<EmailDraft>
{
    public void Configure(EntityTypeBuilder<EmailDraft> builder)
    {
        builder.ToTable("EmailDrafts");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(d => d.Reason).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.HasOne<Workspace>().WithMany().HasForeignKey(d => d.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        // In the database: composite (WorkspaceId, QuestionId) → EmailQuestions (WorkspaceId, Id).
        builder.HasOne<EmailQuestion>().WithOne().HasForeignKey<EmailDraft>(d => d.QuestionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(d => d.DecidedByUserId);
    }
}

public class OutboundEmailConfiguration : IEntityTypeConfiguration<OutboundEmail>
{
    public void Configure(EntityTypeBuilder<OutboundEmail> builder)
    {
        builder.ToTable("OutboundEmails");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Kind).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(o => o.Subject).IsRequired().HasMaxLength(OutboundEmail.MaxSubjectLength);
        builder.Property(o => o.Body).IsRequired();
        builder.Property(o => o.MessageId).IsRequired().HasMaxLength(255);
        builder.Property(o => o.InReplyTo).HasMaxLength(EmailQuestion.MaxMessageIdLength);
        builder.Property(o => o.References).HasMaxLength(EmailQuestion.MaxReferencesLength);
        builder.Property(o => o.CancelReason).HasMaxLength(OutboundEmail.MaxCancelReasonLength);
        builder.Ignore(o => o.IsFinished);

        builder.HasOne<Workspace>().WithMany().HasForeignKey(o => o.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(o => o.ToUserId);
        // In the database: composite same-workspace FKs to EmailThreads and EmailQuestions.
        builder.HasOne<EmailThread>().WithMany().HasForeignKey(o => o.ThreadId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<EmailQuestion>().WithMany().HasForeignKey(o => o.QuestionId).OnDelete(DeleteBehavior.Cascade);
    }
}
