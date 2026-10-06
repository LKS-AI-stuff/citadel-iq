using CitadelIQ.Application;
using CitadelIQ.Application.Documents;
using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Folders;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Application.Options;
using CitadelIQ.Application.Rag;
using CitadelIQ.Application.Search;
using CitadelIQ.Domain.Entities;
using CitadelIQ.Domain.Enums;
using CitadelIQ.FluentMigrations;
using CitadelIQ.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace CitadelIQ.Tests.Support;

/// <summary>
/// A fully wired application (real repositories, services, migrations and file storage) against its own
/// freshly-migrated database inside the shared Postgres container. Only OpenAI (faked) and the background
/// dispatcher (no-op — tests call <c>ProcessDocumentAsync</c> explicitly) are replaced.
/// Each call to <see cref="RunAsync{T}"/> uses a new DI scope, like one HTTP request would.
/// </summary>
public sealed class TestApp : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private readonly string _contentRoot;

    public FakeEmbeddingService Embeddings { get; }
    public FakeChatCompletionService Chat { get; }
    public string ConnectionString { get; }
    public string DocumentsDirectory => Path.Combine(_contentRoot, "documents");

    private TestApp(ServiceProvider services, FakeEmbeddingService embeddings, FakeChatCompletionService chat, string connectionString, string contentRoot)
    {
        _services = services;
        Embeddings = embeddings;
        Chat = chat;
        ConnectionString = connectionString;
        _contentRoot = contentRoot;
    }

    public static async Task<TestApp> CreateAsync(string adminConnectionString, double minSimilarity = 0.25, int chunkSize = 400, int chunkOverlap = 80, double ragMinSimilarity = 0.30, Action<StorageOptions>? configureStorage = null, string storageProvider = StorageOptions.LocalDiskProvider)
    {
        var databaseName = $"t_{Guid.NewGuid():N}";
        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = databaseName }.ConnectionString;
        var contentRoot = Path.Combine(Path.GetTempPath(), "citadeliq-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);

        var embeddings = new FakeEmbeddingService();
        var chat = new FakeChatCompletionService();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:CitadelIQ"] = connectionString, ["Storage:Provider"] = storageProvider })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(contentRoot));
        services.Configure<UploadOptions>(_ => { });
        services.Configure<OpenAIOptions>(_ => { });
        services.Configure<StorageOptions>(o =>
        {
            o.DocumentsPath = "documents";
            o.Provider = storageProvider;
            configureStorage?.Invoke(o);
        });
        services.Configure<ChunkingOptions>(o => { o.ChunkSize = chunkSize; o.ChunkOverlap = chunkOverlap; });
        services.Configure<SearchOptions>(o => o.MinSimilarity = minSimilarity);
        services.Configure<RagOptions>(o => o.MinSimilarity = ragMinSimilarity);
        services.AddApplication();
        services.AddInfrastructure(config);
        services.AddFluentMigrations(connectionString);

        // Replace OpenAI and the fire-and-forget dispatcher (last registration wins).
        services.AddSingleton<IOpenAIEmbeddingService>(embeddings);
        services.AddSingleton<IChatCompletionService>(chat);
        services.AddSingleton<IDocumentProcessingDispatcher, NoOpDispatcher>();

        var provider = services.BuildServiceProvider();
        provider.ApplyDatabaseMigrations();

        return new TestApp(provider, embeddings, chat, connectionString, contentRoot);
    }

    public async Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = _services.CreateScope();
        return await action(scope.ServiceProvider);
    }

    public async Task RunAsync(Func<IServiceProvider, Task> action)
    {
        using var scope = _services.CreateScope();
        await action(scope.ServiceProvider);
    }

    // ---- Application-level helpers ----

    public Task<FolderDto> CreateFolderAsync(Guid parentId, string name) =>
        RunAsync(sp => sp.GetRequiredService<IFolderService>().CreateFolderAsync(parentId, name));

    public Task<DocumentSummaryDto> UploadAsync(Guid folderId, string fileName, byte[] content) =>
        RunAsync(sp => sp.GetRequiredService<IDocumentService>()
            .UploadDocumentAsync(folderId, fileName, "application/octet-stream", new MemoryStream(content), content.Length));

    public Task ProcessAsync(Guid documentId) =>
        RunAsync(sp => sp.GetRequiredService<IDocumentService>().ProcessDocumentAsync(documentId));

    /// <summary>Upload then run the processing pipeline (what the dispatcher does in production).</summary>
    public async Task<Guid> UploadAndProcessAsync(Guid folderId, string fileName, byte[] content)
    {
        var document = await UploadAsync(folderId, fileName, content);
        await ProcessAsync(document.Id);
        return document.Id;
    }

    public Task<DocumentStatusDto> GetStatusAsync(Guid documentId) =>
        RunAsync(sp => sp.GetRequiredService<IDocumentService>().GetStatusAsync(documentId));

    public Task<IReadOnlyList<SearchResultDto>> SearchAsync(string query, Guid folderId, SearchScope scope, int? topK = null) =>
        RunAsync(sp => sp.GetRequiredService<ISearchService>().SearchAsync(new SearchRequestDto(query, folderId, scope, topK)));

    public async Task<(AnswerRun Run, List<AnswerEvent> Events)> AskAsync(AskRequestDto request)
    {
        var events = new List<AnswerEvent>();
        var run = await RunAsync(async sp =>
        {
            var r = await sp.GetRequiredService<IAnswerService>().StartAsync(request);
            await foreach (var e in r.StreamAsync())
            {
                events.Add(e);
            }

            return r;
        });
        return (run, events);
    }

    // ---- Raw SQL helpers (inspect what is really in the database) ----

    public async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync();
        return (T)Convert.ChangeType(result!, typeof(T));
    }

    public async Task<List<object?>> ColumnAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<object?>();
        while (await reader.ReadAsync())
        {
            values.Add(reader.IsDBNull(0) ? null : reader.GetValue(0));
        }

        return values;
    }

    public async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        try
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
        catch (IOException)
        {
            // best-effort temp cleanup
        }
    }

    private sealed class NoOpDispatcher : IDocumentProcessingDispatcher
    {
        public void Dispatch(Guid documentId)
        {
        }
    }

    private sealed class TestHostEnvironment(string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "CitadelIQ.Tests";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
