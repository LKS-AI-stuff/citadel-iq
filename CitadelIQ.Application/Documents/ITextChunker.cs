namespace CitadelIQ.Application.Documents;

public interface ITextChunker
{
    /// <summary>Chunks each section separately (a chunk never spans two sections/pages) and carries the
    /// section's page number / sheet name onto its chunks.</summary>
    IReadOnlyList<TextChunk> Chunk(IReadOnlyList<ExtractedSection> sections);
}
