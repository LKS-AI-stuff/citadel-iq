namespace CitadelIQ.Application.Documents;

/// <summary>A run of extracted text and where it came from. <see cref="PageNumber"/> (1-based) is set for PDFs
/// and <see cref="SheetName"/> for XLSX worksheets; both are null for formats without a reliable location
/// (DOCX, TXT, CSV). At most one of the two is ever set.</summary>
public record ExtractedSection(int? PageNumber, string Text, string? SheetName = null);

/// <summary>One chunk of text ready for embedding, with the page or sheet it came from (when known).</summary>
public record TextChunk(int? PageNumber, string Text, string? SheetName = null);
