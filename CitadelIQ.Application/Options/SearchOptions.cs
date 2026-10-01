namespace CitadelIQ.Application.Options;

public class SearchOptions
{
    public int DefaultTopK { get; set; } = 10;

    public int MaxTopK { get; set; } = 50;

    /// <summary>Minimum cosine similarity (0–1) a chunk needs to be returned; weaker matches are dropped, so a
    /// query the documents don't cover returns nothing instead of the K nearest irrelevant chunks. 0 disables
    /// the filter. The default is a starting value for <c>text-embedding-3-small</c> (related text typically
    /// scores ~0.3–0.6, unrelated below ~0.2) — tune it per model/chunk size; override via user-secrets
    /// (<c>Search:MinSimilarity</c>) or the <c>Search__MinSimilarity</c> environment variable.</summary>
    public double MinSimilarity { get; set; } = 0.25;
}
