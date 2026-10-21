namespace CitadelIQ.Domain.Entities;

/// <summary>One numbered source an email answer was written from (stored as JSON on the question).
/// <see cref="Cited"/> marks the ones the answer actually cites — only those are listed in a reply.</summary>
public record EmailSourceRef(
    int Number,
    Guid DocumentId,
    Guid FolderId,
    string FileName,
    int? PageNumber,
    string? SheetName,
    double Similarity,
    bool Cited);
