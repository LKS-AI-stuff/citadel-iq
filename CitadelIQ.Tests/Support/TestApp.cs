using CitadelIQ.Application;
using CitadelIQ.Application.Accounts;
using CitadelIQ.Application.Documents;
using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Folders;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Application.Options;
using CitadelIQ.Application.Organizations;
using CitadelIQ.Application.Rag;
using CitadelIQ.Application.Search;
using CitadelIQ.Domain.Enums;
using CitadelIQ.FluentMigrations;
using CitadelIQ.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace CitadelIQ.Tests.Support;

/// <summary>A signed-in identity in tests. Each <see cref="TestApp.RunAsAsync{T}"/> resolves it from the database,
/// exactly like the API's current-user middleware does per request.</summary>
public sealed record TestUser(Guid Id, string Issuer, string Subject, string DisplayName, string Email);

/// <summary>
/// A fully wired application (real repositories, services, migrations and file storage) against its own
/// freshly-migrated database inside the shared Postgres container. Only OpenAI (faked) and the background
/// dispatcher (no-op — tests call <c>ProcessDocumentAsync</c> explicitly) are replaced.
/// Migrations run as the container superuser; the services run as the restricted <c>citadeliq_app</c> role, so
/// row-level security is enforced in every test. Each run uses a new DI scope, like one HTTP request.
/// A default user with an individual workspace (<see cref="DefaultUser"/>, <see cref="Root"/>) is created up front,
/// so single-workspace tests read like they did before workspaces existed.
/// </summary>
public sealed class TestApp : IAsyncDisposable
{
    public const string TestIssuer = "https://issuer.test";

    private readonly ServiceProvider _services;
    private readonly string _contentRoot;

    public FakeEmbeddingService Embeddings { get; }
    public FakeChatCompletionService Chat { get; }

    /// <summary>The restricted runtime role's connection string (what the API uses).</summary>
    public string AppConnectionString { get; }

    /// <summary>Superuser connection string for this test's database: migrations and raw inspection (bypasses RLS).</summary>
    public string AdminConnectionString { get; }

    public string DocumentsDirectory => Path.Combine(_contentRoot, "documents");

    public TestUser DefaultUser { get; private set; } = null!;
    public Guid DefaultWorkspaceId { get; private set; }
    /// <summary>The default workspace's root ("Home") folder.</summary>
    public Guid Root { get; private set; }

    private TestApp(ServiceProvider services, FakeEmbeddingService embeddings, FakeChatCompletionService chat, string appConnectionString, string adminConnectionString, string contentRoot)
    {
        _services = services;
        Embeddings = embeddings;
        Chat = chat;
        AppConnectionString = appConnectionString;
        AdminConnectionString = adminConnectionString;
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

        var adminDb = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = databaseName }.ConnectionString;
        var appDb = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Database = databaseName,
            Username = PostgresFixture.AppRole,
            Password = PostgresFixture.AppRolePassword
        }.ConnectionString;

        var contentRoot = Path.Combine(Path.GetTempPath(), "citadeliq-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);

        var embeddings = new FakeEmbeddingService();
        var chat = new FakeChatCompletionService();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:CitadelIQ"] = appDb, ["Storage:Provider"] = storageProvider })
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
        services.AddFluentMigrations(adminDb);

        // Replace OpenAI and the fire-and-forget dispatcher (last registration wins).
        services.AddSingleton<IOpenAIEmbeddingService>(embeddings);
        services.AddSingleton<IChatCompletionService>(chat);
        services.AddSingleton<IDocumentProcessingDispatcher, NoOpDispatcher>();

        var provider = services.BuildServiceProvider();
        provider.ApplyDatabaseMigrations();

        var app = new TestApp(provider, embeddings, chat, appDb, adminDb, contentRoot);
        app.DefaultUser = await app.CreateUserAsync("Default User");
        var session = await app.OnboardIndividualAsync(app.DefaultUser);
        app.DefaultWorkspaceId = session.Workspace!.Id;
        app.Root = session.Workspace.RootFolderId;
        return app;
    }

    // ---- Scopes ----

    /// <summary>Runs in a new scope as <paramref name="user"/> (resolved from the database like a request), or
    /// anonymously when null.</summary>
    public async Task<T> RunAsAsync<T>(TestUser? user, Func<IServiceProvider, Task<T>> action)
    {
        using var scope = _services.CreateScope();
        if (user is not null)
        {
            var resolved = await scope.ServiceProvider.GetRequiredService<IAccountService>().ResolveAsync(user.Issuer, user.Subject)
                ?? throw new InvalidOperationException($"Unknown test user {user.DisplayName}.");
            scope.ServiceProvider.GetRequiredService<CurrentUserContext>().SetUser(resolved.User, resolved.Membership, resolved.Workspace);
        }

        return await action(scope.ServiceProvider);
    }

    public Task RunAsAsync(TestUser? user, Func<IServiceProvider, Task> action) =>
        RunAsAsync<object?>(user, async sp =>
        {
            await action(sp);
            return null;
        });

    /// <summary>Runs as the default user.</summary>
    public Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> action) => RunAsAsync(DefaultUser, action);

    public Task RunAsync(Func<IServiceProvider, Task> action) => RunAsAsync(DefaultUser, action);

    /// <summary>Runs like background work: no user, only a workspace entered.</summary>
    public async Task RunInWorkspaceAsync(Guid workspaceId, Func<IServiceProvider, Task> action)
    {
        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IWorkspaceContext>().Enter(workspaceId);
        await action(scope.ServiceProvider);
    }

    // ---- Accounts and organizations ----

    public async Task<TestUser> CreateUserAsync(string displayName)
    {
        var subject = Guid.NewGuid().ToString("N");
        var email = $"{displayName.Replace(' ', '.').ToLowerInvariant()}.{subject[..6]}@example.test";
        var user = await RunAsAsync(null, sp => sp.GetRequiredService<IAccountService>().EnsureUserAsync(TestIssuer, subject, email, displayName));
        return new TestUser(user.Id, TestIssuer, subject, user.DisplayName, user.Email);
    }

    public Task<SessionDto> SessionAsync(TestUser user) =>
        RunAsAsync(user, sp => sp.GetRequiredService<IAccountService>().GetSessionAsync());

    public Task<SessionDto> OnboardIndividualAsync(TestUser user) =>
        RunAsAsync(user, sp => sp.GetRequiredService<IOnboardingService>().CreateIndividualAsync());

    public Task<SessionDto> CreateOrganizationAsync(TestUser owner, string name) =>
        RunAsAsync(owner, sp => sp.GetRequiredService<IOnboardingService>().CreateOrganizationAsync(name));

    public async Task<string> GetJoinCodeAsync(TestUser admin) =>
        (await RunAsAsync(admin, sp => sp.GetRequiredService<IOrganizationAdminService>().GetJoinCodeAsync())).Code;

    public Task<SessionDto> RequestToJoinAsync(TestUser user, string joinCode) =>
        RunAsAsync(user, sp => sp.GetRequiredService<IOnboardingService>().RequestToJoinAsync(joinCode));

    public Task<IReadOnlyList<JoinRequestDto>> ListJoinRequestsAsync(TestUser admin) =>
        RunAsAsync(admin, sp => sp.GetRequiredService<IOrganizationAdminService>().ListJoinRequestsAsync());

    public Task<MemberDto> ApproveAsync(TestUser admin, Guid requestId, WorkspaceRole role = WorkspaceRole.Member) =>
        RunAsAsync(admin, sp => sp.GetRequiredService<IOrganizationAdminService>().ApproveJoinRequestAsync(requestId, role));

    /// <summary>New user → join request with the org's code → approved by <paramref name="admin"/> with <paramref name="role"/>.</summary>
    public async Task<TestUser> AddMemberAsync(TestUser admin, string displayName, WorkspaceRole role = WorkspaceRole.Member)
    {
        var user = await CreateUserAsync(displayName);
        await RequestToJoinAsync(user, await GetJoinCodeAsync(admin));
        var request = (await ListJoinRequestsAsync(admin)).Single(r => r.Email == user.Email);
        await ApproveAsync(admin, request.Id, role);
        return user;
    }

    /// <summary>A new user who owns a new organization; returns the owner and the organization's root folder.</summary>
    public async Task<(TestUser Owner, Guid WorkspaceId, Guid Root)> CreateOrganizationWithOwnerAsync(string organizationName, string ownerName = "Owner")
    {
        var owner = await CreateUserAsync(ownerName);
        var session = await CreateOrganizationAsync(owner, organizationName);
        return (owner, session.Workspace!.Id, session.Workspace.RootFolderId);
    }

    // ---- Application-level helpers (default user unless one is given) ----

    public Task<FolderDto> CreateFolderAsync(Guid parentId, string name, TestUser? user = null) =>
        RunAsAsync(user ?? DefaultUser, sp => sp.GetRequiredService<IFolderService>().CreateFolderAsync(parentId, name));

    public Task<DocumentSummaryDto> UploadAsync(Guid folderId, string fileName, byte[] content, TestUser? user = null) =>
        RunAsAsync(user ?? DefaultUser, sp => sp.GetRequiredService<IDocumentService>()
            .UploadDocumentAsync(folderId, fileName, "application/octet-stream", new MemoryStream(content), content.Length));

    /// <summary>Runs the pipeline the way the dispatcher does: in the document's workspace (here: the user's).</summary>
    public Task ProcessAsync(Guid documentId, TestUser? user = null) =>
        RunAsAsync(user ?? DefaultUser, sp => sp.GetRequiredService<IDocumentService>().ProcessDocumentAsync(documentId));

    /// <summary>Upload then run the processing pipeline (what the dispatcher does in production).</summary>
    public async Task<Guid> UploadAndProcessAsync(Guid folderId, string fileName, byte[] content, TestUser? user = null)
    {
        var document = await UploadAsync(folderId, fileName, content, user);
        await ProcessAsync(document.Id, user);
        return document.Id;
    }

    public Task<DocumentStatusDto> GetStatusAsync(Guid documentId, TestUser? user = null) =>
        RunAsAsync(user ?? DefaultUser, sp => sp.GetRequiredService<IDocumentService>().GetStatusAsync(documentId));

    public Task<IReadOnlyList<SearchResultDto>> SearchAsync(string query, Guid folderId, SearchScope scope, int? topK = null, TestUser? user = null) =>
        RunAsAsync(user ?? DefaultUser, sp => sp.GetRequiredService<ISearchService>().SearchAsync(new SearchRequestDto(query, folderId, scope, topK)));

    public async Task<(AnswerRun Run, List<AnswerEvent> Events)> AskAsync(AskRequestDto request, TestUser? user = null)
    {
        var events = new List<AnswerEvent>();
        var run = await RunAsAsync(user ?? DefaultUser, async sp =>
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

    public string StoredFilePath(Guid workspaceId, Guid documentId, string extension) =>
        Path.Combine(DocumentsDirectory, workspaceId.ToString(), $"{documentId}{extension}");

    // ---- Raw SQL helpers (superuser: inspect what is really in the database, bypassing RLS) ----

    public async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync();
        return (T)Convert.ChangeType(result!, typeof(T));
    }

    public async Task<List<object?>> ColumnAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(AdminConnectionString);
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
        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();

        // Every test has its own database, so idle pooled connections would pile up across the run.
        NpgsqlConnection.ClearPool(new NpgsqlConnection(AppConnectionString));
        NpgsqlConnection.ClearPool(new NpgsqlConnection(AdminConnectionString));
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
        public void Dispatch(Guid workspaceId, Guid documentId)
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
