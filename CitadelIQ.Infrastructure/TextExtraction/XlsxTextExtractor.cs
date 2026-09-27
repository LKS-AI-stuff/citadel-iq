using System.Text;
using CitadelIQ.Application.Interfaces;
using ClosedXML.Excel;

namespace CitadelIQ.Infrastructure.TextExtraction;

public class XlsxTextExtractor : ITextExtractor
{
    public bool CanHandle(string fileExtension) => fileExtension == ".xlsx";

    public Task<string> ExtractAsync(Stream content, CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(content);
        var builder = new StringBuilder();

        foreach (var worksheet in workbook.Worksheets)
        {
            foreach (var row in worksheet.RowsUsed())
            {
                var cells = row.CellsUsed().Select(c => c.GetString());
                builder.AppendLine(string.Join(" | ", cells));
            }
        }

        return Task.FromResult(builder.ToString());
    }
}
