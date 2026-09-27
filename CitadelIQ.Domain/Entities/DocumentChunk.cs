namespace CitadelIQ.Domain.Entities;

/// <summary>
/// A single searchable slice of a document's extracted text. Each chunk gets its own embedding —
/// documents are never embedded as a whole.
/// </summary>
public class DocumentChunk
{
    public Guid Id { get; private set; }
    public Guid DocumentId { get; private set; }
    public int ChunkIndex { get; private set; }
    public string Text { get; private set; }
    public int? PageNumber { get; private set; }

    private DocumentChunk(Guid id, Guid documentId, int chunkIndex, string text, int? pageNumber)
    {
        Id = id;
        DocumentId = documentId;
        ChunkIndex = chunkIndex;
        Text = text;
        PageNumber = pageNumber;
    }

    public static DocumentChunk Create(Guid documentId, int chunkIndex, string text, int? pageNumber = null) =>
        new(Guid.NewGuid(), documentId, chunkIndex, text, pageNumber);
}
