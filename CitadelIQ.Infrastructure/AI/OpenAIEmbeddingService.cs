using CitadelIQ.Application.Interfaces;
using CitadelIQ.Application.Options;
using Microsoft.Extensions.Options;
using OpenAI.Embeddings;

namespace CitadelIQ.Infrastructure.AI;

public class OpenAIEmbeddingService(IOptions<OpenAIOptions> options) : IOpenAIEmbeddingService
{
    private readonly Lazy<EmbeddingClient> _client = new(() =>
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
        {
            throw new InvalidOperationException("OpenAI API key is not configured.");
        }

        return new EmbeddingClient(options.Value.EmbeddingModel, options.Value.ApiKey);
    });

    public string ModelName => options.Value.EmbeddingModel;

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var result = await _client.Value.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);
        return result.Value.ToFloats().ToArray();
    }

    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        var result = await _client.Value.GenerateEmbeddingsAsync(texts, cancellationToken: cancellationToken);
        return result.Value.Select(e => e.ToFloats().ToArray()).ToList();
    }
}
