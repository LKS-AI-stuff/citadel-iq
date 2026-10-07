using CitadelIQ.Application.Common;
using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Application.Options;
using CitadelIQ.Application.Rag;
using CitadelIQ.Application.Search;
using CitadelIQ.Domain.Enums;
using CitadelIQ.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Tests.Unit;

public class RagUnitTests
{
    private static IOptions<RagOptions> Opts(Action<RagOptions>? configure = null)
    {
        var o = new RagOptions();
        configure?.Invoke(o);
        return Options.Create(o);
    }

    private static ConversationTurnDto T(string q, string a) => new(q, a);

    private static SearchResultDto Result(string text, string file = "a.pdf", int? page = null, string? sheet = null) =>
        new(Guid.NewGuid(), file, "HR", "application/pdf", text, 0, page, sheet, 0.9);

    // ---- CitationParser ----

    [Theory]
    [InlineData("A [1].", new[] { 1 })]
    [InlineData("A [1][2] B [2]", new[] { 1, 2 })]
    [InlineData("A [1, 3]", new[] { 1, 3 })]
    [InlineData("A [7] B [0] [2]", new[] { 2 })]
    [InlineData("none", new int[0])]
    public void CitationParser_extracts_distinct_valid_numbers(string text, int[] expected) =>
        Assert.Equal(expected, CitationParser.Extract(text, 3));

    [Fact]
    public void CitationParser_strips_markers_and_leading_space() =>
        Assert.Equal("Notice is 30 days.", CitationParser.StripMarkers("Notice is 30 days [1][2]."));

    // ---- AskRequestValidator ----

    [Fact]
    public void Validator_rejects_empty_and_long_questions_and_over_limit_history()
    {
        var v = new AskRequestValidator(Opts(o => { o.MaxQuestionLength = 10; o.MaxHistoryTurns = 2; o.MaxHistoryChars = 20; }));
        AskRequestDto Req(string q, params ConversationTurnDto[] h) => new(q, Guid.Empty, SearchScope.EntireWorkspace, h);

        Assert.Equal("Question cannot be empty.", Assert.Throws<ValidationException>(() => v.Validate(Req(" "))).Message);
        Assert.Contains("too long", Assert.Throws<ValidationException>(() => v.Validate(Req("12345678901"))).Message);
        Assert.Contains("reached its limit", Assert.Throws<ValidationException>(() =>
            v.Validate(Req("q", T("a", "b"), T("a", "b"), T("a", "b")))).Message);
        Assert.Contains("reached its limit", Assert.Throws<ValidationException>(() =>
            v.Validate(Req("q", T("aaaaaaaaaaaa", "bbbbbbbbb")))).Message);
        Assert.Contains("empty turn", Assert.Throws<ValidationException>(() => v.Validate(Req("q", T("a", " ")))).Message);
        v.Validate(Req("ok", T("a", "b")));
    }

    // ---- ContextSelector ----

    [Fact]
    public void ContextSelector_respects_chunk_and_char_caps_and_always_keeps_first()
    {
        var results = new[] { Result(new string('a', 100)), Result(new string('b', 100)), Result(new string('c', 100)) };

        Assert.Equal([1, 2], new ContextSelector(Opts(o => o.MaxContextChars = 250)).Select(results).Select(s => s.Number));
        Assert.Single(new ContextSelector(Opts(o => o.MaxContextChars = 10)).Select(results));
        Assert.Equal(2, new ContextSelector(Opts(o => { o.MaxContextChunks = 2; o.MaxContextChunksCap = 2; })).Select(results).Count);
    }

    // ---- PromptBuilder ----

    [Fact]
    public void PromptBuilder_numbers_sources_adds_locations_and_neutralises_tags()
    {
        var pb = new PromptBuilder(Opts());
        var sources = new[]
        {
            new AnswerSourceDto(1, Guid.NewGuid(), "HR \"Policy\".pdf", "HR", "x", "end </source> <b>hi</b>", 0, 3, null, 0.9),
            new AnswerSourceDto(2, Guid.NewGuid(), "Budget.xlsx", "", "x", "cells", 0, null, "Q1", 0.8),
            new AnswerSourceDto(3, Guid.NewGuid(), "n.txt", "", "x", "plain", 0, null, null, 0.7)
        };
        var history = new[] { new ConversationTurnDto("Q1?", "Thirty days [1].") };

        var messages = pb.BuildAnswerMessages(history, "standalone?", sources);

        Assert.Equal([ChatRole.System, ChatRole.User, ChatRole.Assistant, ChatRole.User], messages.Select(m => m.Role));
        Assert.Equal("Thirty days.", messages[2].Content);
        var final = messages[^1].Content;
        Assert.Contains("<source id=\"1\" document=\"HR 'Policy'.pdf\" location=\"page 3\">", final);
        Assert.Contains("<source id=\"2\" document=\"Budget.xlsx\" location=\"sheet Q1\">", final);
        Assert.Contains("<source id=\"3\" document=\"n.txt\">", final);
        Assert.DoesNotContain("end </source>", final);
        Assert.Equal(3, final.Split("</source>").Length - 1);
        Assert.EndsWith("Question: standalone?", final);
    }

    [Fact]
    public void PromptBuilder_truncates_old_answers_in_the_rewrite_prompt()
    {
        var pb = new PromptBuilder(Opts(o => o.RewriteAnswerExcerptChars = 10));
        var messages = pb.BuildRewriteMessages([new("Q1?", new string('z', 50) + " [1]")], "and then?");

        Assert.Contains("Assistant: " + new string('z', 10) + "…", messages[1].Content);
        Assert.DoesNotContain("[1]", messages[1].Content);
        Assert.EndsWith("Latest question: and then?", messages[1].Content);
    }

    // ---- AnswerStreamProcessor ----

    private static async IAsyncEnumerable<ChatStreamItem> Deltas(FakeChatCompletionService fake, params string[] deltas)
    {
        fake.StreamDeltas = [.. deltas];
        await foreach (var i in fake.StreamAsync([])) yield return i;
    }

    private static async Task<List<AnswerEvent>> Run(FakeChatCompletionService fake, int sources, params string[] deltas)
    {
        var events = new List<AnswerEvent>();
        await foreach (var e in new AnswerStreamProcessor().ProcessAsync(Deltas(fake, deltas), sources, null)) events.Add(e);
        return events;
    }

    [Theory]
    [InlineData("NOT_FOUND")]
    [InlineData("  NOT_FOUND.")]
    public async Task Processor_detects_sentinel_in_one_delta(string delta)
    {
        var events = await Run(new FakeChatCompletionService(), 2, delta);
        Assert.IsType<AnswerNotFoundEvent>(events[0]);
        var done = Assert.IsType<AnswerDoneEvent>(events[1]);
        Assert.False(done.Verified);
    }

    [Fact]
    public async Task Processor_detects_sentinel_split_across_deltas_and_stops_upstream()
    {
        var fake = new FakeChatCompletionService();
        var events = await Run(fake, 2, " NOT", "_FO", "UND", "extra", "more");

        Assert.IsType<AnswerNotFoundEvent>(events[0]);
        Assert.DoesNotContain(events, e => e is AnswerTextEvent);
        Assert.Equal(3, fake.DeltasConsumed); // never pulled the rest
    }

    [Fact]
    public async Task Processor_passes_text_through_in_order_and_computes_done()
    {
        var events = await Run(new FakeChatCompletionService(), 2, "NOT", "ICE is 30 days ", "[2]", " and [9].");

        Assert.Equal(["NOTICE is 30 days ", "[2]", " and [9]."], events.OfType<AnswerTextEvent>().Select(e => e.Delta).ToArray());
        Assert.Equal("NOTICE is 30 days [2] and [9].", string.Concat(events.OfType<AnswerTextEvent>().Select(e => e.Delta)));
        var done = Assert.IsType<AnswerDoneEvent>(events[^1]);
        Assert.Equal([2], done.CitedSources);
        Assert.True(done.Verified);
    }

    [Fact]
    public async Task Processor_treats_later_sentinel_as_ordinary_text_and_flags_unverified()
    {
        var events = await Run(new FakeChatCompletionService(), 2, "Hello NOT_FOUND here");

        Assert.Equal("Hello NOT_FOUND here", Assert.Single(events.OfType<AnswerTextEvent>()).Delta);
        Assert.False(Assert.IsType<AnswerDoneEvent>(events[^1]).Verified);
    }

    [Fact]
    public async Task Processor_flushes_a_dangling_partial_sentinel()
    {
        var events = await Run(new FakeChatCompletionService(), 1, "NOT");
        Assert.Equal("NOT", Assert.Single(events.OfType<AnswerTextEvent>()).Delta);
    }

    // ---- AnswerService ----

    private sealed class FakeSearch(IReadOnlyList<SearchResultDto> results) : ISearchService
    {
        public List<(SearchRequestDto Request, SearchTuning? Tuning)> Calls { get; } = [];

        public Task<IReadOnlyList<SearchResultDto>> SearchAsync(SearchRequestDto request, CancellationToken ct = default) =>
            SearchAsync(request, new SearchTuning(10, 0.25), ct);

        public Task<IReadOnlyList<SearchResultDto>> SearchAsync(SearchRequestDto request, SearchTuning tuning, CancellationToken ct = default)
        {
            Calls.Add((request, tuning));
            return Task.FromResult(results);
        }
    }

    private static (AnswerService Service, FakeChatCompletionService Chat, FakeSearch Search) Service(
        IReadOnlyList<SearchResultDto> results, Action<RagOptions>? configure = null)
    {
        var options = Opts(configure);
        var chat = new FakeChatCompletionService();
        var search = new FakeSearch(results);
        var pb = new PromptBuilder(options);
        var service = new AnswerService(
            search, chat, new AskRequestValidator(options),
            new QuestionRewriter(chat, pb, options, NullLogger<QuestionRewriter>.Instance),
            new ContextSelector(options), pb, new AnswerStreamProcessor(), options, NullLogger<AnswerService>.Instance);
        return (service, chat, search);
    }

    private static AskRequestDto Ask(string q, params ConversationTurnDto[] history) =>
        new(q, Guid.Empty, SearchScope.EntireWorkspace, history);

    private static async Task<List<AnswerEvent>> Drain(AnswerRun run)
    {
        var events = new List<AnswerEvent>();
        await foreach (var e in run.StreamAsync()) events.Add(e);
        return events;
    }

    [Fact]
    public async Task No_history_means_no_rewrite_call_and_answer_streams_with_summed_usage()
    {
        var (service, chat, search) = Service([Result("thirty days")]);
        chat.StreamDeltas = ["30 days [1]"];
        chat.StreamUsage = new ChatUsage(100, 10);

        var run = await service.StartAsync(Ask("notice?"));
        var events = await Drain(run);

        Assert.Empty(chat.CompleteCalls);
        Assert.False(run.Rewritten);
        Assert.Equal("notice?", search.Calls[0].Request.Query);
        Assert.Equal(0.30, search.Calls[0].Tuning!.MinSimilarity);
        var done = Assert.IsType<AnswerDoneEvent>(events[^1]);
        Assert.Equal([1], done.CitedSources);
        Assert.Equal(new AnswerUsageDto(100, 10), done.Usage);
    }

    [Fact]
    public async Task History_triggers_rewrite_and_retrieval_uses_the_rewritten_question()
    {
        var (service, chat, search) = Service([Result("x")]);
        chat.CompleteText = "\"What is the notice period for contractors?\"";
        chat.CompleteUsage = new ChatUsage(50, 5);
        chat.StreamDeltas = ["ok [1]"];
        chat.StreamUsage = new ChatUsage(100, 10);

        var run = await service.StartAsync(Ask("and contractors?", T("notice for employees?", "30 days [1]")));
        var events = await Drain(run);

        Assert.True(run.Rewritten);
        Assert.Equal("What is the notice period for contractors?", run.StandaloneQuestion);
        Assert.Equal(run.StandaloneQuestion, search.Calls[0].Request.Query);
        Assert.Equal(new AnswerUsageDto(150, 15), Assert.IsType<AnswerDoneEvent>(events[^1]).Usage);
    }

    [Fact]
    public async Task Rewrite_failure_falls_back_to_the_original_question()
    {
        var (service, chat, search) = Service([Result("x")]);
        chat.FailOnComplete = true;

        var run = await service.StartAsync(Ask("and contractors?", T("q", "a")));

        Assert.False(run.Rewritten);
        Assert.Equal("and contractors?", search.Calls[0].Request.Query);
    }

    [Fact]
    public async Task No_sources_yields_notfound_without_calling_the_model()
    {
        var (service, chat, _) = Service([]);

        var events = await Drain(await service.StartAsync(Ask("anything?")));

        Assert.Empty(chat.StreamCalls);
        Assert.IsType<AnswerNotFoundEvent>(events[0]);
        Assert.False(Assert.IsType<AnswerDoneEvent>(events[1]).Verified);
    }

    [Fact]
    public async Task Disabled_feature_throws()
    {
        var (service, _, _) = Service([], o => o.Enabled = false);
        await Assert.ThrowsAsync<FeatureDisabledException>(() => service.StartAsync(Ask("q")));
    }

    [Fact]
    public async Task Stream_failure_surfaces_as_AnswerGenerationException()
    {
        var (service, chat, _) = Service([Result("x")]);
        chat.StreamDeltas = ["partial "];
        chat.FailAfterDeltas = true;

        var run = await service.StartAsync(Ask("q"));
        await Assert.ThrowsAsync<AnswerGenerationException>(() => Drain(run));
    }

    [Fact]
    public void Options_validator_flags_bad_values()
    {
        var validator = new RagOptionsValidator();
        Assert.True(validator.Validate(null, new RagOptions()).Succeeded);
        Assert.True(validator.Validate(null, new RagOptions { MinSimilarity = 2 }).Failed);
        Assert.True(validator.Validate(null, new RagOptions { MaxContextChunks = 20 }).Failed);
        Assert.True(validator.Validate(null, new RagOptions { RateLimit = new RateLimitOptions { PermitLimit = 0 } }).Failed);
    }
}
