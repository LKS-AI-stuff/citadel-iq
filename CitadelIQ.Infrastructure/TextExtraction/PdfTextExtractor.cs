using CitadelIQ.Application.Documents;
using CitadelIQ.Application.Interfaces;
using UglyToad.PdfPig;

namespace CitadelIQ.Infrastructure.TextExtraction;

public class PdfTextExtractor : ITextExtractor
{
    public bool CanHandle(string fileExtension) => fileExtension == ".pdf";

    public Task<IReadOnlyList<ExtractedSection>> ExtractAsync(Stream content, CancellationToken cancellationToken = default)
    {
        using var document = PdfDocument.Open(content);

        // One section per page (1-based page number); blank pages are skipped.
        IReadOnlyList<ExtractedSection> sections = document.GetPages()
            .Where(page => !string.IsNullOrWhiteSpace(page.Text))
            .Select(page => new ExtractedSection(page.Number, page.Text))
            .ToList();

        return Task.FromResult(sections);
    }
}
