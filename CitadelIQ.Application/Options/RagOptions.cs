namespace CitadelIQ.Application.Options;

/// <summary>Settings for retrieval-augmented answers (<c>Rag</c> section). All limits are configuration, never constants.</summary>
public class RagOptions
{
    /// <summary>Kill switch: when false the answer endpoint returns 503.</summary>
    public bool Enabled { get; set; } = true;

    public int MaxContextChunks { get; set; } = 8;

    /// <summary>Hard ceiling for <see cref="MaxContextChunks"/>.</summary>
    public int MaxContextChunksCap { get; set; } = 12;

    public int MaxContextChars { get; set; } = 8000;

    /// <summary>Minimum cosine similarity for a chunk to be used as context. Stricter than search's default because
    /// wrong context produces wrong answers. Untuned starting value.</summary>
    public double MinSimilarity { get; set; } = 0.30;

    public int MaxQuestionLength { get; set; } = 1000;

    public int MaxHistoryTurns { get; set; } = 10;

    public int MaxHistoryChars { get; set; } = 12000;

    /// <summary>Optional. Sent to the model only when set (some models reject it or count hidden reasoning tokens against it).</summary>
    public int? MaxOutputTokens { get; set; }

    /// <summary>Optional. Sent to the model only when set (some models reject non-default values).</summary>
    public double? Temperature { get; set; }

    /// <summary>Past answers are truncated to this many characters in the rewrite prompt.</summary>
    public int RewriteAnswerExcerptChars { get; set; } = 500;

    public RateLimitOptions RateLimit { get; set; } = new();
}

public class RateLimitOptions
{
    public int PermitLimit { get; set; } = 20;

    public int WindowSeconds { get; set; } = 60;

    public int MaxConcurrentStreams { get; set; } = 2;
}
