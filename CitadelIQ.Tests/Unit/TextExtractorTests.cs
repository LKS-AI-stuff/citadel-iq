using CitadelIQ.Application.Common;
using CitadelIQ.Infrastructure.TextExtraction;
using CitadelIQ.Tests.Support;

namespace CitadelIQ.Tests.Unit;

public class TextExtractorTests
{
    [Fact]
    public async Task Pdf_returns_one_section_per_non_blank_page_with_one_based_page_numbers()
    {
        var pdf = TestFiles.Pdf("alpha page", "", "gamma page");

        var sections = await new PdfTextExtractor().ExtractAsync(new MemoryStream(pdf));

        Assert.Equal([1, 3], sections.Select(s => s.PageNumber));
        Assert.Contains("alpha", sections[0].Text);
        Assert.Contains("gamma", sections[1].Text);
        Assert.All(sections, s => Assert.Null(s.SheetName));
    }

    [Fact]
    public async Task Xlsx_returns_one_section_per_non_empty_sheet_with_the_sheet_name()
    {
        var xlsx = TestFiles.Xlsx(
            ("Budget", ["rent", "salaries"]),
            ("Empty", []),
            ("Headcount", ["engineering"]));

        var sections = await new XlsxTextExtractor().ExtractAsync(new MemoryStream(xlsx));

        Assert.Equal(["Budget", "Headcount"], sections.Select(s => s.SheetName));
        Assert.Contains("salaries", sections[0].Text);
        Assert.All(sections, s => Assert.Null(s.PageNumber));
    }

    [Fact]
    public async Task Plain_text_returns_a_single_section_without_a_location()
    {
        var sections = await new PlainTextExtractor().ExtractAsync(new MemoryStream(TestFiles.Text("hello")));

        var section = Assert.Single(sections);
        Assert.Equal("hello", section.Text);
        Assert.Null(section.PageNumber);
        Assert.Null(section.SheetName);
    }

    [Fact]
    public async Task Unsupported_file_type_is_rejected_by_the_extraction_service()
    {
        var service = new TextExtractionService([new PlainTextExtractor()]);

        await Assert.ThrowsAsync<DocumentProcessingException>(() =>
            service.ExtractAsync(new MemoryStream([1]), ".exe"));
    }
}
