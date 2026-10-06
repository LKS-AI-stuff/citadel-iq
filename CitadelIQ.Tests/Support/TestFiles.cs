using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace CitadelIQ.Tests.Support;

/// <summary>Builds small real PDF / XLSX / text files in memory for upload tests.</summary>
public static class TestFiles
{
    public static byte[] Text(string content) => Encoding.UTF8.GetBytes(content);

    /// <summary>One PDF page per argument; an empty string produces a blank page.</summary>
    public static byte[] Pdf(params string[] pages)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        foreach (var text in pages)
        {
            var page = builder.AddPage(595, 842);
            if (text.Length > 0)
            {
                page.AddText(text, 12, new(30, 780), font);
            }
        }

        return builder.Build();
    }

    /// <summary>A DOCX with one paragraph per argument.</summary>
    public static byte[] Docx(params string[] paragraphs)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new Document(new Body(paragraphs.Select(p => new Paragraph(new Run(new Text(p))))));
            main.Document.Save();
        }

        return stream.ToArray();
    }

    /// <summary>One worksheet per argument; a sheet with no cell texts stays empty.</summary>
    public static byte[] Xlsx(params (string Sheet, string[] Cells)[] sheets)
    {
        using var workbook = new XLWorkbook();

        foreach (var (name, cells) in sheets)
        {
            var sheet = workbook.AddWorksheet(name);
            for (var row = 0; row < cells.Length; row++)
            {
                sheet.Cell(row + 1, 1).Value = cells[row];
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
