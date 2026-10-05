using System.Net;
using Microsoft.AspNetCore.Hosting;
using System.Net.Http.Json;
using System.Text.Json;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Domain.Entities;
using CitadelIQ.Tests.Support;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace CitadelIQ.Tests.Integration;

/// <summary>Drives the real ASP.NET pipeline (SSE framing, rate limiter, middleware) with fake OpenAI services.</summary>
[Collection(PostgresCollection.Name)]
public class AnswersApiTests(PostgresFixture postgres)
{
    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
        public FakeChatCompletionService Chat { get; } = new();
        public FakeEmbeddingService Embeddings { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing"); // don't pick up the developer's user-secrets
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IChatCompletionService>(Chat);
                services.AddSingleton<IOpenAIEmbeddingService>(Embeddings);
            });
        }
    }

    /// <summary>Program.cs reads configuration eagerly, so settings are passed as environment variables that
    /// exist only while the host is built.</summary>
    private static ApiFactory CreateFactory(string connectionString, Dictionary<string, string>? settings = null)
    {
        var env = new Dictionary<string, string>(settings ?? []) { ["ConnectionStrings__CitadelIQ"] = connectionString };
        foreach (var (k, v) in env) Environment.SetEnvironmentVariable(k, v);
        try
        {
            var factory = new ApiFactory();
            _ = factory.Server; // build the host while the variables are set
            return factory;
        }
        finally
        {
            foreach (var k in env.Keys) Environment.SetEnvironmentVariable(k, null);
        }
    }

    private static HttpRequestMessage AskRequest(string question, string? scope = null, object[]? history = null) =>
        new(HttpMethod.Post, "/api/answers/stream")
        {
            Content = JsonContent.Create(new
            {
                question,
                currentFolderId = Folder.RootId,
                searchScope = scope ?? "EntirePortal",
                history = history ?? []
            })
        };

    private static List<(string Name, JsonElement Data)> ParseSse(string body) =>
        body.Split("\n\n", StringSplitOptions.RemoveEmptyEntries).Select(frame =>
        {
            var lines = frame.Split('\n');
            Assert.StartsWith("event: ", lines[0]);
            Assert.StartsWith("data: ", lines[1]);
            return (lines[0][7..], JsonDocument.Parse(lines[1][6..]).RootElement.Clone());
        }).ToList();

    private async Task<(TestApp App, ApiFactory Factory)> SetUpAsync(Dictionary<string, string>? settings = null, bool seed = true)
    {
        var app = await postgres.CreateAppAsync();
        if (seed)
        {
            await app.UploadAndProcessAsync(Folder.RootId, "finance.txt", TestFiles.Text("revenue grew"));
        }

        return (app, CreateFactory(app.ConnectionString, settings));
    }

    [Fact]
    public async Task Normal_answer_streams_events_in_order()
    {
        var (app, factory) = await SetUpAsync();
        await using var _ = app;
        using var __ = factory;
        factory.Chat.StreamDeltas = ["Revenue ", "grew [1]."];
        factory.Chat.StreamUsage = new ChatUsage(10, 5);

        var response = await factory.CreateClient().SendAsync(AskRequest("revenue?"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/event-stream", response.Content.Headers.ContentType!.ToString());
        Assert.Equal("no", response.Headers.GetValues("X-Accel-Buffering").Single());
        var events = ParseSse(await response.Content.ReadAsStringAsync());
        Assert.Equal(["question", "sources", "text", "text", "done"], events.Select(e => e.Name));
        Assert.Equal("revenue?", events[0].Data.GetProperty("standaloneQuestion").GetString());
        Assert.False(events[0].Data.GetProperty("rewritten").GetBoolean());
        var source = events[1].Data.GetProperty("sources")[0];
        Assert.Equal(1, source.GetProperty("number").GetInt32());
        Assert.Equal("finance.txt", source.GetProperty("fileName").GetString());
        Assert.Equal("Revenue ", events[2].Data.GetProperty("delta").GetString());
        Assert.Equal([1], events[4].Data.GetProperty("citedSources").EnumerateArray().Select(e => e.GetInt32()));
        Assert.True(events[4].Data.GetProperty("verified").GetBoolean());
        Assert.Equal(10, events[4].Data.GetProperty("usage").GetProperty("inputTokens").GetInt32());
    }

    [Fact]
    public async Task Sentinel_produces_notfound_then_done()
    {
        var (app, factory) = await SetUpAsync();
        await using var _ = app;
        using var __ = factory;
        factory.Chat.StreamDeltas = ["NOT_", "FOUND"];

        var response = await factory.CreateClient().SendAsync(AskRequest("revenue?"));

        var events = ParseSse(await response.Content.ReadAsStringAsync());
        Assert.Equal(["question", "sources", "notfound", "done"], events.Select(e => e.Name));
        Assert.False(events[3].Data.GetProperty("verified").GetBoolean());
    }

    [Fact]
    public async Task No_sources_yields_notfound_without_calling_the_model()
    {
        var (app, factory) = await SetUpAsync(seed: false);
        await using var _ = app;
        using var __ = factory;

        var response = await factory.CreateClient().SendAsync(AskRequest("revenue?"));

        var events = ParseSse(await response.Content.ReadAsStringAsync());
        Assert.Equal(["question", "sources", "notfound", "done"], events.Select(e => e.Name));
        Assert.Equal(0, events[1].Data.GetProperty("sources").GetArrayLength());
        Assert.Empty(factory.Chat.StreamCalls);
    }

    [Fact]
    public async Task Upstream_failure_after_start_becomes_an_error_event()
    {
        var (app, factory) = await SetUpAsync();
        await using var _ = app;
        using var __ = factory;
        factory.Chat.StreamDeltas = ["partial "];
        factory.Chat.FailAfterDeltas = true;

        var response = await factory.CreateClient().SendAsync(AskRequest("revenue?"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var events = ParseSse(await response.Content.ReadAsStringAsync());
        Assert.Equal("error", events[^1].Name);
        Assert.Equal("An error occurred while generating the answer.", events[^1].Data.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Pre_stream_errors_are_problem_details()
    {
        var (app, factory) = await SetUpAsync();
        await using var _ = app;
        using var __ = factory;
        var client = factory.CreateClient();

        var empty = await client.SendAsync(AskRequest("  "));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal("application/problem+json", empty.Content.Headers.ContentType!.MediaType);

        var unknownFolder = await client.PostAsJsonAsync("/api/answers/stream", new
        {
            question = "q", currentFolderId = Guid.NewGuid(), searchScope = "CurrentFolder", history = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.NotFound, unknownFolder.StatusCode);
    }

    [Fact]
    public async Task Malformed_and_over_limit_requests_are_400()
    {
        var (app, factory) = await SetUpAsync();
        await using var _ = app;
        using var __ = factory;
        var client = factory.CreateClient();

        var nullTurn = await client.PostAsync("/api/answers/stream", new StringContent(
            "{\"question\":\"q\",\"currentFolderId\":\"00000000-0000-0000-0000-000000000000\",\"searchScope\":\"EntirePortal\",\"history\":[null]}",
            System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, nullTurn.StatusCode);

        var tooLong = await client.SendAsync(AskRequest(new string('x', 1001)));
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);

        var tooManyTurns = await client.SendAsync(AskRequest("q", history: Enumerable.Range(0, 11).Select(_ => (object)new { question = "a", answer = "b" }).ToArray()));
        Assert.Equal(HttpStatusCode.BadRequest, tooManyTurns.StatusCode);
        // The 64 KB [RequestSizeLimit] is enforced by Kestrel, which TestServer doesn't emulate, so the
        // 413 mapping in ExceptionHandlingMiddleware can't be exercised here.
    }

    [Fact]
    public async Task Disabled_feature_returns_503()
    {
        var (app, factory) = await SetUpAsync(new() { ["Rag__Enabled"] = "false" });
        await using var _ = app;
        using var __ = factory;

        var response = await factory.CreateClient().SendAsync(AskRequest("revenue?"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var settings = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/settings");
        Assert.False(settings.GetProperty("askEnabled").GetBoolean());
    }

    [Fact]
    public async Task Settings_returns_only_the_client_safe_fields()
    {
        var (app, factory) = await SetUpAsync(seed: false);
        await using var _ = app;
        using var __ = factory;

        var settings = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/settings");

        Assert.Equal(["askEnabled", "maxHistoryChars", "maxHistoryTurns", "maxQuestionLength"],
            settings.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.Equal(1000, settings.GetProperty("maxQuestionLength").GetInt32());
    }

    [Fact]
    public async Task Exceeding_the_request_window_returns_429_with_problem_details()
    {
        var (app, factory) = await SetUpAsync(new() { ["Rag__RateLimit__PermitLimit"] = "2" });
        await using var _ = app;
        using var __ = factory;
        factory.Chat.StreamDeltas = ["ok [1]"];
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(AskRequest("revenue?"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(AskRequest("revenue?"))).StatusCode);
        var limited = await client.SendAsync(AskRequest("revenue?"));

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Contains("too quickly", await limited.Content.ReadAsStringAsync());
        // other endpoints are not limited
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/settings")).StatusCode);
    }

    [Fact]
    public async Task A_parallel_stream_over_the_concurrency_cap_is_rejected_and_disconnect_cancels_upstream()
    {
        var (app, factory) = await SetUpAsync(new() { ["Rag__RateLimit__MaxConcurrentStreams"] = "1" });
        await using var _ = app;
        using var __ = factory;
        factory.Chat.StreamGate = new TaskCompletionSource();
        var client = factory.CreateClient();

        // Headers arrive with the first SSE event, then the model call blocks on the gate.
        var first = await client.SendAsync(AskRequest("revenue?"), HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        await WaitUntilAsync(() => factory.Chat.StreamCalls.Count == 1);

        var second = await client.SendAsync(AskRequest("revenue?"));
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);

        first.Dispose(); // client disconnects
        await WaitUntilAsync(() => factory.Chat.StreamCancelled);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for condition.");
            await Task.Delay(25);
        }
    }
}
