using CitadelIQ.Application.Interfaces;

namespace CitadelIQ.Infrastructure.TextExtraction;

public class PlainTextExtractor : ITextExtractor
{
    public bool CanHandle(string fileExtension) => fileExtension is ".txt" or ".csv";

    public async Task<string> ExtractAsync(Stream content, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(content);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
