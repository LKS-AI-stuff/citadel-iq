using System.Text;
using CitadelIQ.Application.Documents;
using CitadelIQ.Application.Interfaces;
using ClosedXML.Excel;

namespace CitadelIQ.Infrastructure.TextExtraction;

public class XlsxTextExtractor : ITextExtractor
{
    public bool CanHandle(string fileExtension) => fileExtension == ".xlsx";

    public Task<IReadOnlyList<ExtractedSection>> ExtractAsync(Stream content, CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(content);
        var sections = new List<ExtractedSection>();

        // One section per worksheet so each chunk carries its sheet name; empty sheets are skipped.
        foreach (var worksheet in workbook.Worksheets)
        {
            var builder = new StringBuilder();

            foreach (var row in worksheet.RowsUsed())
            {
                var cells = row.CellsUsed().Select(c => c.GetString());
                builder.AppendLine(string.Join(" | ", cells));
            }

            if (!string.IsNullOrWhiteSpace(builder.ToString()))
            {
                sections.Add(new ExtractedSection(null, builder.ToString(), worksheet.Name));
            }
        }

        return Task.FromResult<IReadOnlyList<ExtractedSection>>(sections);
    }
}
