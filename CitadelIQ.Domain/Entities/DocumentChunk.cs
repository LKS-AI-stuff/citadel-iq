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
    /// <summary>XLSX worksheet the chunk came from; null for other formats. Never set together with <see cref="PageNumber"/>.</summary>
    public string? SheetName { get; private set; }

    private DocumentChunk(Guid id, Guid documentId, int chunkIndex, string text, int? pageNumber, string? sheetName)
    {
        Id = id;
        DocumentId = documentId;
        ChunkIndex = chunkIndex;
        Text = text;
        PageNumber = pageNumber;
        SheetName = sheetName;
    }

    public static DocumentChunk Create(Guid documentId, int chunkIndex, string text, int? pageNumber = null, string? sheetName = null) =>
        new(Guid.NewGuid(), documentId, chunkIndex, text, pageNumber, sheetName);
}
