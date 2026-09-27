namespace CitadelIQ.Application.Documents;

public interface ITextChunker
{
    IReadOnlyList<string> Chunk(string text);
}
