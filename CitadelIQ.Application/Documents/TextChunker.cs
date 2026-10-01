using CitadelIQ.Application.Options;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Application.Documents;

/// <summary>Splits extracted text into overlapping, roughly-sized chunks for embedding — never one
/// embedding for a whole document (see requirements §6). Chunk size/overlap are configurable.
/// Sections (PDF pages, XLSX sheets) are chunked independently, so a chunk has a single page/sheet and
/// overlap does not carry across a page or sheet boundary.</summary>
public class TextChunker(IOptions<ChunkingOptions> options) : ITextChunker
{
    public IReadOnlyList<TextChunk> Chunk(IReadOnlyList<ExtractedSection> sections)
    {
        var chunks = new List<TextChunk>();

        foreach (var section in sections)
        {
            chunks.AddRange(Split(section.Text).Select(text => new TextChunk(section.PageNumber, text, section.SheetName)));
        }

        return chunks;
    }

    private List<string> Split(string text)
    {
        var chunkSize = options.Value.ChunkSize;
        var overlap = Math.Min(options.Value.ChunkOverlap, chunkSize - 1);
        var normalized = text.Trim();

        if (normalized.Length == 0)
        {
            return [];
        }

        if (normalized.Length <= chunkSize)
        {
            return [normalized];
        }

        var chunks = new List<string>();
        var start = 0;

        while (start < normalized.Length)
        {
            var length = Math.Min(chunkSize, normalized.Length - start);
            var chunk = normalized.Substring(start, length).Trim();

            if (chunk.Length > 0)
            {
                chunks.Add(chunk);
            }

            if (start + length >= normalized.Length)
            {
                break;
            }

            start += chunkSize - overlap;
        }

        return chunks;
    }
}
