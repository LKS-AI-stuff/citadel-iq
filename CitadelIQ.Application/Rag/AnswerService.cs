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
    public async Task<AnswerRun> StartAsync(AskRequestDto request, CancellationToken cancellationToken = default)
    {
        var rag = options.Value;

        if (!rag.Enabled)
        {
            throw new FeatureDisabledException("Answers are currently turned off.");
        }

        validator.Validate(request);

        var history = request.History ?? [];
        var question = request.Question.Trim();

        var rewrite = await rewriter.RewriteAsync(question, history, cancellationToken);

        var topK = Math.Clamp(rag.MaxContextChunks, 1, rag.MaxContextChunksCap);
        var results = await searchService.SearchAsync(
            new SearchRequestDto(rewrite.Question, request.CurrentFolderId, request.SearchScope, topK),
            new SearchTuning(topK, rag.MinSimilarity),
            cancellationToken);

        var sources = contextSelector.Select(results);

        logger.LogInformation(
            "Answer prepared: {HistoryTurns} history turns, rewritten {Rewritten}, {Retrieved} retrieved, {Selected} selected",
            history.Count, rewrite.Rewritten, results.Count, sources.Count);

        return new AnswerRun(rewrite.Question, rewrite.Rewritten, sources, history, rewrite.Usage, chat, promptBuilder, processor, logger);
    }
}
