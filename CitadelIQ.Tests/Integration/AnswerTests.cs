using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Rag;
using CitadelIQ.Domain.Entities;
using CitadelIQ.Domain.Enums;
using CitadelIQ.Tests.Support;

namespace CitadelIQ.Tests.Integration;

[Collection(PostgresCollection.Name)]
public class AnswerTests(PostgresFixture postgres)
{
    private static AskRequestDto Ask(string q, Guid folder, SearchScope scope = SearchScope.EntirePortal, params ConversationTurnDto[] history) =>
        new(q, folder, scope, history);

    [Fact]
    public async Task Answer_uses_real_retrieval_and_numbers_sources()
    {
        await using var app = await postgres.CreateAppAsync();
        await app.UploadAndProcessAsync(Folder.RootId, "finance.txt", TestFiles.Text("revenue grew"));
        await app.UploadAndProcessAsync(Folder.RootId, "hr.txt", TestFiles.Text("vacation policy"));
        app.Chat.StreamDeltas = ["Revenue grew [1]."];

        var (run, events) = await app.AskAsync(Ask("revenue?", Folder.RootId));

        var source = Assert.Single(run.Sources);
        Assert.Equal(1, source.Number);
        Assert.Equal("finance.txt", source.FileName);
        Assert.Equal([1], Assert.IsType<AnswerDoneEvent>(events[^1]).CitedSources);
    }

    [Fact]
    public async Task Follow_up_retrieves_with_the_rewritten_question()
    {
        await using var app = await postgres.CreateAppAsync();
        await app.UploadAndProcessAsync(Folder.RootId, "finance.txt", TestFiles.Text("revenue grew"));
        await app.UploadAndProcessAsync(Folder.RootId, "hr.txt", TestFiles.Text("vacation policy"));
        app.Chat.CompleteText = "What about vacation?";
        app.Chat.StreamDeltas = ["ok [1]"];

        var (run, _) = await app.AskAsync(Ask("and that?", Folder.RootId, history: new ConversationTurnDto("revenue?", "grew")));

        Assert.True(run.Rewritten);
        Assert.Equal("hr.txt", Assert.Single(run.Sources).FileName);
    }

    [Fact]
    public async Task Scope_is_honoured_and_only_ready_documents_are_used()
    {
        await using var app = await postgres.CreateAppAsync();
        var sub = await app.CreateFolderAsync(Folder.RootId, "Sub");
        await app.UploadAndProcessAsync(Folder.RootId, "root.txt", TestFiles.Text("revenue root"));
        await app.UploadAndProcessAsync(sub.Id, "sub.txt", TestFiles.Text("revenue sub"));
        await app.UploadAsync(sub.Id, "pending.txt", TestFiles.Text("revenue pending")); // never processed
        app.Chat.StreamDeltas = ["x [1]"];

        var (current, _) = await app.AskAsync(Ask("revenue", Folder.RootId, SearchScope.CurrentFolder));
        var (all, _) = await app.AskAsync(Ask("revenue", Folder.RootId, SearchScope.CurrentFolderAndSubfolders));

        Assert.Equal(["root.txt"], current.Sources.Select(s => s.FileName));
        Assert.Equal(["root.txt", "sub.txt"], all.Sources.Select(s => s.FileName).Order());
    }

    [Fact]
    public async Task Rag_min_similarity_is_stricter_than_search()
    {
        await using var app = await postgres.CreateAppAsync(minSimilarity: 0.25, ragMinSimilarity: 0.8);
        await app.UploadAndProcessAsync(Folder.RootId, "partial.txt", TestFiles.Text("revenue vacation")); // 0.707

        var search = await app.SearchAsync("revenue", Folder.RootId, SearchScope.EntirePortal);
        var (run, events) = await app.AskAsync(Ask("revenue", Folder.RootId));

        Assert.Single(search);
        Assert.Empty(run.Sources);
        Assert.IsType<AnswerNotFoundEvent>(events[0]);
        Assert.Empty(app.Chat.StreamCalls);
    }

    [Fact]
    public async Task Sources_carry_page_or_sheet_locations()
    {
        await using var app = await postgres.CreateAppAsync();
        await app.UploadAndProcessAsync(Folder.RootId, "sheet.csv", TestFiles.Text("revenue"));
        app.Chat.StreamDeltas = ["x [1]"];

        var (run, _) = await app.AskAsync(Ask("revenue", Folder.RootId));

        var source = Assert.Single(run.Sources);
        Assert.Null(source.PageNumber); // plain text has no location; PDFs/XLSX are covered by SearchTests
        Assert.Equal(0, source.ChunkIndex);
    }
}
