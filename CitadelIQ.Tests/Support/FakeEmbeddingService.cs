using CitadelIQ.Application.Interfaces;

namespace CitadelIQ.Tests.Support;

/// <summary>
/// Deterministic stand-in for OpenAI: each known topic word is one axis of the vector, so tests control
/// exactly which chunk is similar to which query. "revenue" vs "revenue" = 1.0, "revenue" vs "vacation" = 0,
/// "revenue" vs "revenue vacation" ≈ 0.707. Text with no topic word maps to a shared "other" axis.
/// </summary>
public class FakeEmbeddingService : IOpenAIEmbeddingService
{
    public const int Dimension = 1536;
    private static readonly string[] Topics = ["revenue", "vacation", "security", "weather"];

    public string ModelName => "fake-model";

    /// <summary>When true, any embedding call throws — simulates an OpenAI outage.</summary>
    public bool FailOnGenerate { get; set; }

    public static float[] Embed(string text)
    {
        var vector = new float[Dimension];
        var lower = text.ToLowerInvariant();
        var matched = 0;

        for (var i = 0; i < Topics.Length; i++)
        {
            if (lower.Contains(Topics[i]))
            {
                vector[i] = 1;
                matched++;
            }
        }

        if (matched == 0)
        {
            vector[Topics.Length] = 1;
            matched = 1;
        }

        var norm = (float)Math.Sqrt(matched);
        for (var i = 0; i <= Topics.Length; i++)
        {
            vector[i] /= norm;
        }

        return vector;
    }

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        ThrowIfFailing();
        return Task.FromResult(Embed(text));
    }

    public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        ThrowIfFailing();
        IReadOnlyList<float[]> vectors = texts.Select(Embed).ToList();
        return Task.FromResult(vectors);
    }

    private void ThrowIfFailing()
    {
        if (FailOnGenerate)
        {
            throw new InvalidOperationException("Simulated embedding failure.");
        }
    }
}
