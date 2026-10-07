using CitadelIQ.Application.Common;
using CitadelIQ.Domain.Enums;
using CitadelIQ.Tests.Support;

namespace CitadelIQ.Tests.Integration;

[Collection(PostgresCollection.Name)]
public class SearchTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Results_are_ranked_by_similarity_and_scored_as_cosine_similarity()
    {
        await using var app = await postgres.CreateAppAsync(minSimilarity: 0);
        await app.UploadAndProcessAsync(app.Root, "exact.txt", TestFiles.Text("revenue"));
        await app.UploadAndProcessAsync(app.Root, "partial.txt", TestFiles.Text("revenue vacation"));
        await app.UploadAndProcessAsync(app.Root, "unrelated.txt", TestFiles.Text("vacation"));

        var results = await app.SearchAsync("revenue", app.Root, SearchScope.EntireWorkspace);

        Assert.Equal(["exact.txt", "partial.txt", "unrelated.txt"], results.Select(r => r.FileName));
        Assert.Equal(1.0, results[0].SimilarityScore, 3);
        Assert.Equal(0.707, results[1].SimilarityScore, 3);
        Assert.Equal(0.0, results[2].SimilarityScore, 3);
    }

    [Fact]
    public async Task TopK_limits_the_number_of_results()
    {
        await using var app = await postgres.CreateAppAsync(minSimilarity: 0);
        await app.UploadAndProcessAsync(app.Root, "exact.txt", TestFiles.Text("revenue"));
        await app.UploadAndProcessAsync(app.Root, "partial.txt", TestFiles.Text("revenue vacation"));

        var results = await app.SearchAsync("revenue", app.Root, SearchScope.EntireWorkspace, topK: 1);

        Assert.Equal("exact.txt", Assert.Single(results).FileName);
    }

    [Fact]
    public async Task Chunks_below_the_minimum_similarity_are_excluded()
    {
        await using var app = await postgres.CreateAppAsync(minSimilarity: 0.8);
        await app.UploadAndProcessAsync(app.Root, "exact.txt", TestFiles.Text("revenue"));
        await app.UploadAndProcessAsync(app.Root, "partial.txt", TestFiles.Text("revenue vacation")); // 0.707 < 0.8

        var results = await app.SearchAsync("revenue", app.Root, SearchScope.EntireWorkspace);

        Assert.Equal("exact.txt", Assert.Single(results).FileName);
    }

    [Fact]
    public async Task A_query_nothing_matches_returns_an_empty_list_with_the_default_threshold()
    {
        await using var app = await postgres.CreateAppAsync(); // default 0.25
        await app.UploadAndProcessAsync(app.Root, "vacation.txt", TestFiles.Text("vacation days"));

        var results = await app.SearchAsync("revenue", app.Root, SearchScope.EntireWorkspace);

        Assert.Empty(results);
    }

    [Fact]
    public async Task A_threshold_of_zero_disables_the_filter()
    {
        await using var app = await postgres.CreateAppAsync(minSimilarity: 0);
        await app.UploadAndProcessAsync(app.Root, "vacation.txt", TestFiles.Text("vacation days"));

        var results = await app.SearchAsync("revenue", app.Root, SearchScope.EntireWorkspace);

        Assert.Single(results);
    }

    [Fact]
    public async Task Search_scopes_return_different_documents_and_the_folder_path_is_displayed()
    {
        await using var app = await postgres.CreateAppAsync();
        var finance = await app.CreateFolderAsync(app.Root, "Finance");
        var reports = await app.CreateFolderAsync(finance.Id, "Reports");
        var hr = await app.CreateFolderAsync(app.Root, "HR");
        await app.UploadAndProcessAsync(finance.Id, "finance.txt", TestFiles.Text("revenue"));
        await app.UploadAndProcessAsync(reports.Id, "reports.txt", TestFiles.Text("revenue"));
        await app.UploadAndProcessAsync(hr.Id, "hr.txt", TestFiles.Text("revenue"));

        var thisFolder = await app.SearchAsync("revenue", finance.Id, SearchScope.CurrentFolder);
        var withSubfolders = await app.SearchAsync("revenue", finance.Id, SearchScope.CurrentFolderAndSubfolders);
        var everything = await app.SearchAsync("revenue", hr.Id, SearchScope.EntireWorkspace);
        var reportsOnly = await app.SearchAsync("revenue", reports.Id, SearchScope.CurrentFolder);

        Assert.Equal(["finance.txt"], thisFolder.Select(r => r.FileName));
        Assert.Equal(["finance.txt", "reports.txt"], withSubfolders.Select(r => r.FileName).Order());
        Assert.Equal(["finance.txt", "hr.txt", "reports.txt"], everything.Select(r => r.FileName).Order());
        Assert.Equal("Finance / Reports", Assert.Single(reportsOnly).FolderPath);
    }

    [Fact]
    public async Task Only_ready_documents_are_searchable()
    {
        await using var app = await postgres.CreateAppAsync();
        await app.UploadAndProcessAsync(app.Root, "ready.txt", TestFiles.Text("revenue"));
        await app.UploadAsync(app.Root, "pending.txt", TestFiles.Text("revenue")); // never processed
        app.Embeddings.FailOnGenerate = true;
        await app.UploadAndProcessAsync(app.Root, "failed.txt", TestFiles.Text("revenue"));
        app.Embeddings.FailOnGenerate = false;

        var results = await app.SearchAsync("revenue", app.Root, SearchScope.EntireWorkspace);

        Assert.Equal("ready.txt", Assert.Single(results).FileName);
    }

    [Fact]
    public async Task Results_include_the_page_number_for_pdfs_and_the_sheet_name_for_xlsx()
    {
        await using var app = await postgres.CreateAppAsync();
        await app.UploadAndProcessAsync(app.Root, "report.pdf", TestFiles.Pdf("weather", "revenue on page two"));
        await app.UploadAndProcessAsync(app.Root, "plan.xlsx", TestFiles.Xlsx(("Weather", ["weather"]), ("Money", ["revenue figures"])));

        var results = await app.SearchAsync("revenue", app.Root, SearchScope.EntireWorkspace);

        var pdf = Assert.Single(results, r => r.FileName == "report.pdf");
        Assert.Equal(2, pdf.PageNumber);
        Assert.Null(pdf.SheetName);

        var xlsx = Assert.Single(results, r => r.FileName == "plan.xlsx");
        Assert.Equal("Money", xlsx.SheetName);
        Assert.Null(xlsx.PageNumber);
    }

    [Fact]
    public async Task Empty_queries_and_unknown_folders_are_rejected()
    {
        await using var app = await postgres.CreateAppAsync();

        await Assert.ThrowsAsync<ValidationException>(() => app.SearchAsync("  ", app.Root, SearchScope.EntireWorkspace));
        await Assert.ThrowsAsync<NotFoundException>(() => app.SearchAsync("revenue", Guid.NewGuid(), SearchScope.CurrentFolder));
    }
}
