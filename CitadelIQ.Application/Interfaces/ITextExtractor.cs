namespace CitadelIQ.Application.Interfaces;

/// <summary>One strategy for extracting text from a specific file type (PDF, DOCX, XLSX, plain text).</summary>
public interface ITextExtractor
{
    bool CanHandle(string fileExtension);

    Task<string> ExtractAsync(Stream content, CancellationToken cancellationToken = default);
}
