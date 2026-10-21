using CitadelIQ.Application.Common;
using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Application.Options;
using CitadelIQ.Application.Search;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Application.Rag;

public class AnswerService(
    ISearchService searchService,
    IChatCompletionService chat,
    AskRequestValidator validator,
    QuestionRewriter rewriter,
    ContextSelector contextSelector,
    PromptBuilder promptBuilder,
    AnswerStreamProcessor processor,
    IOptions<RagOptions> options,
    ILogger<AnswerService> logger) : IAnswerService
{
    public Task<AnswerRun> StartAsync(AskRequestDto request, CancellationToken cancellationToken = default) =>
        StartAsync(request, null, cancellationToken);

    public async Task<AnswerRun> StartAsync(AskRequestDto request, AnswerRetrieval? retrieval, CancellationToken cancellationToken = default)
    {
        var rag = options.Value;

        if (!rag.Enabled)
        {
            throw new FeatureDisabledException("Answers are currently turned off.");
        }

        validator.Validate(request);

        var history = request.History ?? [];
        var question = request.Question.Trim();

        var rewrite = retrieval is { QuestionIsStandalone: true }
            ? new RewriteResult(question, false, null)
            : await rewriter.RewriteAsync(question, history, cancellationToken);

        var topK = Math.Clamp(rag.MaxContextChunks, 1, rag.MaxContextChunksCap);
        var tuning = new SearchTuning(topK, rag.MinSimilarity);
        var queries = new List<string> { rewrite.Question };
        queries.AddRange((retrieval?.AdditionalQueries ?? []).Where(q => !string.IsNullOrWhiteSpace(q)));

        IReadOnlyList<SearchResultDto> results = [];
        foreach (var query in queries)
        {
            var found = await searchService.SearchAsync(
                new SearchRequestDto(query, request.CurrentFolderId, request.SearchScope, topK), tuning, cancellationToken);
            results = results.Count == 0 ? found : Merge(results, found);
        }

        var sources = contextSelector.Select(results);

        logger.LogInformation(
            "Answer prepared: {HistoryTurns} history turns, rewritten {Rewritten}, {Queries} queries, {Retrieved} retrieved, {Selected} selected",
            history.Count, rewrite.Rewritten, queries.Count, results.Count, sources.Count);

        return new AnswerRun(rewrite.Question, rewrite.Rewritten, sources, history, rewrite.Usage, chat, promptBuilder, processor, logger);
    }

    /// <summary>Union of two result lists, keeping each chunk's best similarity, re-sorted best first.</summary>
    private static IReadOnlyList<SearchResultDto> Merge(IReadOnlyList<SearchResultDto> a, IReadOnlyList<SearchResultDto> b) =>
        a.Concat(b)
            .GroupBy(r => (r.DocumentId, r.ChunkIndex))
            .Select(g => g.MaxBy(r => r.SimilarityScore)!)
            .OrderByDescending(r => r.SimilarityScore)
            .ToList();
}
