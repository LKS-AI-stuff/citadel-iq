using System.Text;
using CitadelIQ.Application.Interfaces;
using UglyToad.PdfPig;

namespace CitadelIQ.Infrastructure.TextExtraction;

public class PdfTextExtractor : ITextExtractor
{
    public bool CanHandle(string fileExtension) => fileExtension == ".pdf";

    public Task<string> ExtractAsync(Stream content, CancellationToken cancellationToken = default)
    {
        using var document = PdfDocument.Open(content);
        var builder = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            builder.AppendLine(page.Text);
        }

        return Task.FromResult(builder.ToString());
    }
}
