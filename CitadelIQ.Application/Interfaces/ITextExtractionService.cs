namespace CitadelIQ.Application.Interfaces;

/// <summary>Resolves the right <see cref="ITextExtractor"/> for a file extension and extracts its text.</summary>
public interface ITextExtractionService
{
    Task<string> ExtractAsync(Stream content, string fileExtension, CancellationToken cancellationToken = default);
}
