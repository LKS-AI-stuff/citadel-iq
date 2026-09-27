using CitadelIQ.Application.Common;
using CitadelIQ.Application.Interfaces;

namespace CitadelIQ.Infrastructure.TextExtraction;

public class TextExtractionService(IEnumerable<ITextExtractor> extractors) : ITextExtractionService
{
    public Task<string> ExtractAsync(Stream content, string fileExtension, CancellationToken cancellationToken = default)
    {
        var extractor = extractors.FirstOrDefault(e => e.CanHandle(fileExtension))
            ?? throw new DocumentProcessingException("This file type is not supported.");

        return extractor.ExtractAsync(content, cancellationToken);
    }
}
