namespace CitadelIQ.Domain.Entities;

/// <summary>
/// A single searchable slice of a document's extracted text. Each chunk gets its own embedding —
/// documents are never embedded as a whole.
/// </summary>
public class DocumentChunk
{
    public Guid Id { get; private set; }
    /// <summary>Copied from the document so the vector search (and row-level security) filters on the chunk row itself.</summary>
    public Guid WorkspaceId { get; private set; }
    public Guid DocumentId { get; private set; }
    public int ChunkIndex { get; private set; }
    public string Text { get; private set; }
    public int? PageNumber { get; private set; }
    /// <summary>XLSX worksheet the chunk came from; null for other formats. Never set together with <see cref="PageNumber"/>.</summary>
    public string? SheetName { get; private set; }

    private DocumentChunk(Guid id, Guid workspaceId, Guid documentId, int chunkIndex, string text, int? pageNumber, string? sheetName)
    {
        Id = id;
        WorkspaceId = workspaceId;
        DocumentId = documentId;
        ChunkIndex = chunkIndex;
        Text = text;
        PageNumber = pageNumber;
        SheetName = sheetName;
    }

    public static DocumentChunk Create(Document document, int chunkIndex, string text, int? pageNumber = null, string? sheetName = null) =>
        new(Guid.NewGuid(), document.WorkspaceId, document.Id, chunkIndex, text, pageNumber, sheetName);
}
