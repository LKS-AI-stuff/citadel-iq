using CitadelIQ.Application.Documents;

namespace CitadelIQ.Application.Interfaces;

/// <summary>One strategy for extracting text from a specific file type (PDF, DOCX, XLSX, plain text).</summary>
public interface ITextExtractor
{
    bool CanHandle(string fileExtension);

    /// <summary>Returns the document's text as sections — one per page for PDFs, a single section
    /// (no page number) for formats without page boundaries.</summary>
    Task<IReadOnlyList<ExtractedSection>> ExtractAsync(Stream content, CancellationToken cancellationToken = default);
}
