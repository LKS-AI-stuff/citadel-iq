using CitadelIQ.Application.Interfaces;
using CitadelIQ.Domain.Entities;
using CitadelIQ.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CitadelIQ.Infrastructure.Persistence;

// Workspace tables: every query below is confined to the scope's workspace by the query filters (and, independently,
// by row-level security on the connection).

public class EmailThreadRepository(CitadelIQDbContext db) : IEmailThreadRepository
{
    public Task<EmailThread?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.EmailThreads.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task AddAsync(EmailThread thread, CancellationToken cancellationToken = default)
    {
        db.EmailThreads.Add(thread);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(EmailThread thread, CancellationToken cancellationToken = default)
    {
        if (db.Entry(thread).State == EntityState.Detached)
        {
            db.EmailThreads.Update(thread);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<int> DeleteInactiveAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default) =>
        db.EmailThreads.Where(t => t.LastActivityAtUtc < cutoffUtc).ExecuteDeleteAsync(cancellationToken);
}

public class EmailQuestionRepository(CitadelIQDbContext db) : IEmailQuestionRepository
{
    public async Task AddAsync(EmailQuestion question, CancellationToken cancellationToken = default)
    {
        db.EmailQuestions.Add(question);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<EmailQuestion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.EmailQuestions.AsNoTracking().FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

    public async Task<IReadOnlyList<EmailThreadEntry>> ListThreadAsync(Guid threadId, CancellationToken cancellationToken = default)
    {
        var rows = await (from question in db.EmailQuestions.AsNoTracking()
                          where question.ThreadId == threadId
                          join draft in db.EmailDrafts.AsNoTracking() on question.Id equals draft.QuestionId into drafts
                          from draft in drafts.DefaultIfEmpty()
                          orderby question.ReceivedAtUtc
                          select new { question, draft })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new EmailThreadEntry(r.question, r.draft)).ToList();
    }

    public Task<int> CountSinceAsync(Guid? senderUserId, DateTimeOffset sinceUtc, CancellationToken cancellationToken = default)
    {
        var query = db.EmailQuestions.Where(q => q.ReceivedAtUtc >= sinceUtc);
        if (senderUserId is { } id)
        {
            query = query.Where(q => q.SenderUserId == id);
        }

        return query.CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmailActivityRecord>> ListActivityAsync(DateTimeOffset? beforeUtc, int skipAtBefore, int limit, Guid? senderUserId, CancellationToken cancellationToken = default)
    {
        var questions = db.EmailQuestions.AsNoTracking();
        if (senderUserId is { } id)
        {
            questions = questions.Where(q => q.SenderUserId == id);
        }

        var skip = 0;
        if (beforeUtc is { } before)
        {
            questions = questions.Where(q => q.ReceivedAtUtc <= before);
            skip = skipAtBefore; // rows after `before` are excluded, so the first rows in this order are the ties at it
        }

        var page = questions.OrderByDescending(q => q.ReceivedAtUtc).ThenByDescending(q => q.Id).Skip(skip).Take(limit);
        var rows = await (from question in page
                          join user in db.Users.AsNoTracking() on question.SenderUserId equals user.Id
                          select new
                          {
                              question,
                              user.DisplayName,
                              // The reply to this question (not a notification), newest first.
                              Delivery = db.OutboundEmails
                                  .Where(o => o.QuestionId == question.Id && o.Kind != OutboundEmailKind.DraftNotification)
                                  .OrderByDescending(o => o.CreatedAtUtc)
                                  .Select(o => (OutboundEmailStatus?)o.Status)
                                  .FirstOrDefault()
                          })
            .ToListAsync(cancellationToken);

        return rows
            .OrderByDescending(r => r.question.ReceivedAtUtc)
            .Select(r => new EmailActivityRecord(r.question, r.DisplayName, r.Delivery))
            .ToList();
    }

    public Task<int> DeleteExpiredAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default) =>
        db.EmailQuestions.Where(q => q.ExpiresAtUtc <= nowUtc).ExecuteDeleteAsync(cancellationToken);
}

public class EmailDraftRepository(CitadelIQDbContext db) : IEmailDraftRepository
{
    public async Task AddAsync(EmailDraft draft, CancellationToken cancellationToken = default)
    {
        db.EmailDrafts.Add(draft);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<EmailDraftRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await Records(db.EmailDrafts.AsNoTracking().Where(d => d.Id == id)).ToListAsync(cancellationToken))
        .Select(ToRecord)
        .FirstOrDefault();

    public async Task<IReadOnlyList<EmailDraftRecord>> ListAsync(EmailDraftStatus status, int limit, CancellationToken cancellationToken = default) =>
        (await Records(db.EmailDrafts.AsNoTracking().Where(d => d.Status == status).OrderByDescending(d => d.CreatedAtUtc).Take(limit))
            .ToListAsync(cancellationToken))
        .Select(ToRecord)
        .OrderByDescending(r => r.Draft.CreatedAtUtc)
        .ToList();

    public Task<int> CountPendingAsync(DateTimeOffset? createdAfterUtc = null, CancellationToken cancellationToken = default) =>
        PendingSince(createdAfterUtc).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<EmailDraftRecord>> ListPendingAsync(DateTimeOffset? createdAfterUtc, int limit, CancellationToken cancellationToken = default) =>
        (await Records(PendingSince(createdAfterUtc).AsNoTracking().OrderBy(d => d.CreatedAtUtc).Take(limit)).ToListAsync(cancellationToken))
        .Select(ToRecord)
        .OrderBy(r => r.Draft.CreatedAtUtc)
        .ToList();

    public async Task<bool> TrySaveDecisionAsync(EmailDraft draft, CancellationToken cancellationToken = default)
    {
        var updated = await db.EmailDrafts
            .Where(d => d.Id == draft.Id && d.Status == EmailDraftStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Status, draft.Status)
                .SetProperty(d => d.SentText, draft.SentText)
                .SetProperty(d => d.DecidedByUserId, draft.DecidedByUserId)
                .SetProperty(d => d.DecidedAtUtc, draft.DecidedAtUtc), cancellationToken);
        return updated == 1;
    }

    public Task<int> ExpireAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default) =>
        db.EmailDrafts
            .Where(d => d.Status == EmailDraftStatus.Pending && d.ExpiresAtUtc <= nowUtc)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Status, EmailDraftStatus.Expired)
                .SetProperty(d => d.SuggestedText, (string?)null)
                .SetProperty(d => d.Missing, (string?)null)
                .SetProperty(d => d.DecidedAtUtc, nowUtc), cancellationToken);

    private IQueryable<EmailDraft> PendingSince(DateTimeOffset? createdAfterUtc) =>
        createdAfterUtc is { } after
            ? db.EmailDrafts.Where(d => d.Status == EmailDraftStatus.Pending && d.CreatedAtUtc > after)
            : db.EmailDrafts.Where(d => d.Status == EmailDraftStatus.Pending);

    private record Row(EmailDraft Draft, EmailQuestion Question, string DisplayName);

    private IQueryable<Row> Records(IQueryable<EmailDraft> drafts) =>
        from draft in drafts
        join question in db.EmailQuestions.AsNoTracking() on draft.QuestionId equals question.Id
        join user in db.Users.AsNoTracking() on question.SenderUserId equals user.Id
        select new Row(draft, question, user.DisplayName);

    private static EmailDraftRecord ToRecord(Row row) => new(row.Draft, row.Question, row.DisplayName);
}

public class OutboundEmailRepository(CitadelIQDbContext db) : IOutboundEmailRepository
{
    public async Task AddAsync(OutboundEmail email, CancellationToken cancellationToken = default)
    {
        db.OutboundEmails.Add(email);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<OutboundEmail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.OutboundEmails.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task UpdateAsync(OutboundEmail email, CancellationToken cancellationToken = default)
    {
        if (db.Entry(email).State == EntityState.Detached)
        {
            db.OutboundEmails.Update(email);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OutboundEmail>> FindByMessageIdsAsync(IReadOnlyCollection<string> messageIds, CancellationToken cancellationToken = default) =>
        await db.OutboundEmails.AsNoTracking().Where(o => messageIds.Contains(o.MessageId)).ToListAsync(cancellationToken);

    public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default) =>
        db.OutboundEmails.Where(o => o.CreatedAtUtc < cutoffUtc).ExecuteDeleteAsync(cancellationToken);
}
