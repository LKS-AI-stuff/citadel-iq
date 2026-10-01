using CitadelIQ.Application.Documents;
using CitadelIQ.Application.Interfaces;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CitadelIQ.Infrastructure.TextExtraction;

public class DocxTextExtractor : ITextExtractor
{
    public bool CanHandle(string fileExtension) => fileExtension == ".docx";

    public Task<IReadOnlyList<ExtractedSection>> ExtractAsync(Stream content, CancellationToken cancellationToken = default)
    {
        using var document = WordprocessingDocument.Open(content, false);
        var body = document.MainDocumentPart?.Document?.Body;

        if (body is null)
        {
            return Task.FromResult<IReadOnlyList<ExtractedSection>>([]);
        }

        var text = string.Join(Environment.NewLine, body.Descendants<Paragraph>().Select(p => p.InnerText));
        return Task.FromResult<IReadOnlyList<ExtractedSection>>([new ExtractedSection(null, text)]);
    }
}
