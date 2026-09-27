namespace CitadelIQ.Application.Interfaces;

public interface IOpenAIEmbeddingService
{
    /// <summary>The configured embedding model name (e.g. "text-embedding-3-small"), for tagging stored embeddings.</summary>
    string ModelName { get; }

    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
}
