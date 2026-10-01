using CitadelIQ.Application.Documents;
using CitadelIQ.Application.Options;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Tests.Unit;

public class TextChunkerTests
{
    private static TextChunker CreateChunker(int size = 100, int overlap = 20) =>
        new(Options.Create(new ChunkingOptions { ChunkSize = size, ChunkOverlap = overlap }));

    [Theory]
    [InlineData("")]
    [InlineData("   \n\t ")]
    public void Blank_text_produces_no_chunks(string text)
    {
        Assert.Empty(CreateChunker().Chunk([new ExtractedSection(null, text)]));
    }

    [Fact]
    public void No_sections_produces_no_chunks()
    {
        Assert.Empty(CreateChunker().Chunk([]));
    }

    [Fact]
    public void Short_text_becomes_a_single_trimmed_chunk()
    {
        var chunks = CreateChunker().Chunk([new ExtractedSection(null, "  hello world  ")]);

        var chunk = Assert.Single(chunks);
        Assert.Equal("hello world", chunk.Text);
    }

    [Fact]
    public void Long_text_is_split_into_overlapping_chunks_no_larger_than_the_chunk_size()
    {
        // 350 chars of non-repeating-looking letters, no whitespace, so trimming never alters a boundary.
        var text = new string(Enumerable.Range(0, 350).Select(i => (char)('a' + i % 26)).ToArray());

        var chunks = CreateChunker(size: 100, overlap: 20).Chunk([new ExtractedSection(null, text)]);

        Assert.Equal(5, chunks.Count); // starts at 0, 80, 160, 240, 320
        Assert.All(chunks, c => Assert.True(c.Text.Length <= 100));
        for (var i = 1; i < chunks.Count; i++)
        {
            var previousTail = chunks[i - 1].Text[^20..];
            Assert.StartsWith(previousTail, chunks[i].Text);
        }
    }

    [Fact]
    public void Chunks_carry_their_sections_page_number_and_never_span_two_sections()
    {
        var pageOne = new string('a', 150);
        var pageTwo = new string('b', 150);

        var chunks = CreateChunker(size: 100, overlap: 20).Chunk(
        [
            new ExtractedSection(1, pageOne),
            new ExtractedSection(2, pageTwo),
        ]);

        Assert.Equal(4, chunks.Count);
        Assert.All(chunks.Where(c => c.PageNumber == 1), c => Assert.DoesNotContain('b', c.Text));
        Assert.All(chunks.Where(c => c.PageNumber == 2), c => Assert.DoesNotContain('a', c.Text));
        Assert.Equal([1, 1, 2, 2], chunks.Select(c => c.PageNumber));
        Assert.All(chunks, c => Assert.Null(c.SheetName));
    }

    [Fact]
    public void Chunks_carry_their_sections_sheet_name()
    {
        var chunks = CreateChunker().Chunk(
        [
            new ExtractedSection(null, "budget rows", "Budget"),
            new ExtractedSection(null, "headcount rows", "Headcount"),
        ]);

        Assert.Equal(["Budget", "Headcount"], chunks.Select(c => c.SheetName));
        Assert.All(chunks, c => Assert.Null(c.PageNumber));
    }
}
