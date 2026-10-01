namespace CitadelIQ.Application.Interfaces;

/// <summary>
/// One chunk ranked by vector similarity, joined with the metadata of its document.
/// <see cref="SimilarityScore"/> is cosine similarity (1 = identical direction) — not a probability.
/// </summary>
public record DocumentSearchResult(
    Guid ChunkId,
    Guid DocumentId,
    Guid FolderId,
    string FileName,
    string ContentType,
    string ChunkText,
    int ChunkIndex,
    int? PageNumber,
    string? SheetName,
    double SimilarityScore);

public interface IVectorSearchRepository
{
    /// <summary>
    /// Returns the <paramref name="topK"/> most similar chunks among <c>Ready</c> documents, ranked by
    /// similarity descending. <paramref name="eligibleFolderIds"/> is the already-resolved folder
    /// filter (scope resolution stays in the Application layer); <c>null</c> means no folder filter.
    /// </summary>
    Task<IReadOnlyList<DocumentSearchResult>> SearchAsync(
        float[] queryEmbedding,
        IReadOnlyCollection<Guid>? eligibleFolderIds,
        int topK,
        CancellationToken cancellationToken = default);
}
