using CitadelIQ.Application.Options;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Application.Documents;

/// <summary>Splits extracted text into overlapping, roughly-sized chunks for embedding — never one
/// embedding for a whole document (see requirements §6). Chunk size/overlap are configurable.</summary>
public class TextChunker(IOptions<ChunkingOptions> options) : ITextChunker
{
    public IReadOnlyList<string> Chunk(string text)
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
