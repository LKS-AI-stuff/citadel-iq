using CitadelIQ.Application.Common;
using CitadelIQ.Domain.Entities;
using CitadelIQ.Domain.Enums;
using CitadelIQ.Tests.Support;
using Npgsql;

namespace CitadelIQ.Tests.Integration;

[Collection(PostgresCollection.Name)]
public class DocumentProcessingTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Text_document_becomes_ready_with_every_chunk_embedded()
    {
        await using var app = await postgres.CreateAppAsync();

        var id = await app.UploadAndProcessAsync(Folder.RootId, "notes.txt", TestFiles.Text("Revenue grew strongly this year."));

        var status = await app.GetStatusAsync(id);
        Assert.Equal(ProcessingStatus.Ready, status.Status);

        var chunks = await app.ScalarAsync<long>("SELECT count(*) FROM \"DocumentChunks\"");
        Assert.True(chunks > 0);
        Assert.Equal(chunks, await app.ScalarAsync<long>("SELECT count(\"Embedding\") FROM \"DocumentChunks\""));
        Assert.Equal("fake-model", await app.ScalarAsync<string>("SELECT DISTINCT \"ModelName\" FROM \"DocumentChunks\""));
        Assert.Equal(1536, await app.ScalarAsync<int>("SELECT vector_dims(\"Embedding\") FROM \"DocumentChunks\" LIMIT 1"));
    }

    [Fact]
    public async Task Pdf_chunks_carry_one_based_page_numbers_and_skip_blank_pages()
    {
        await using var app = await postgres.CreateAppAsync();

        await app.UploadAndProcessAsync(Folder.RootId, "report.pdf", TestFiles.Pdf("revenue page one", "", "vacation page three"));

        var pages = await app.ColumnAsync("SELECT \"PageNumber\" FROM \"DocumentChunks\" ORDER BY \"ChunkIndex\"");
        Assert.Equal([1, 3], pages.Select(p => (int?)p));
        Assert.Equal(0, await app.ScalarAsync<long>("SELECT count(\"SheetName\") FROM \"DocumentChunks\""));
    }

    [Fact]
    public async Task Xlsx_chunks_carry_sheet_names_and_skip_empty_sheets()
    {
        await using var app = await postgres.CreateAppAsync();

        await app.UploadAndProcessAsync(Folder.RootId, "plan.xlsx", TestFiles.Xlsx(
            ("Budget", ["rent revenue"]),
            ("Empty", []),
            ("Headcount", ["engineering"])));

        var sheets = await app.ColumnAsync("SELECT \"SheetName\" FROM \"DocumentChunks\" ORDER BY \"ChunkIndex\"");
        Assert.Equal(["Budget", "Headcount"], sheets.Select(s => (string?)s));
        Assert.Equal(0, await app.ScalarAsync<long>("SELECT count(\"PageNumber\") FROM \"DocumentChunks\""));
    }

    [Fact]
    public async Task Embedding_failure_marks_the_document_failed_and_leaves_no_chunks()
    {
        await using var app = await postgres.CreateAppAsync();
        app.Embeddings.FailOnGenerate = true;

        var id = await app.UploadAndProcessAsync(Folder.RootId, "notes.txt", TestFiles.Text("Revenue grew."));

        var status = await app.GetStatusAsync(id);
        Assert.Equal(ProcessingStatus.Failed, status.Status);
        Assert.Equal("We couldn't process this document. Please try again.", status.FailureReason);
        Assert.Equal(0, await app.ScalarAsync<long>("SELECT count(*) FROM \"DocumentChunks\""));
    }

    [Fact]
    public async Task Document_with_no_extractable_text_fails_with_a_clear_reason()
    {
        await using var app = await postgres.CreateAppAsync();

        var id = await app.UploadAndProcessAsync(Folder.RootId, "blank.txt", TestFiles.Text("   \n  "));

        var status = await app.GetStatusAsync(id);
        Assert.Equal(ProcessingStatus.Failed, status.Status);
        Assert.Contains("empty", status.FailureReason);
        Assert.Equal(0, await app.ScalarAsync<long>("SELECT count(*) FROM \"DocumentChunks\""));
    }

    [Fact]
    public async Task Database_rejects_a_chunk_that_has_both_a_page_and_a_sheet()
    {
        await using var app = await postgres.CreateAppAsync();
        await app.UploadAndProcessAsync(Folder.RootId, "notes.txt", TestFiles.Text("Revenue grew."));

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            app.ExecuteAsync("UPDATE \"DocumentChunks\" SET \"PageNumber\" = 1, \"SheetName\" = 'Budget'"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    [Fact]
    public async Task Upload_rejects_unsupported_file_types_and_missing_folders()
    {
        await using var app = await postgres.CreateAppAsync();

        await Assert.ThrowsAsync<ValidationException>(() => app.UploadAsync(Folder.RootId, "virus.exe", [1, 2, 3]));
        await Assert.ThrowsAsync<NotFoundException>(() => app.UploadAsync(Guid.NewGuid(), "notes.txt", TestFiles.Text("x")));
    }

    [Fact]
    public async Task Deleting_a_document_removes_its_row_chunks_and_stored_file()
    {
        await using var app = await postgres.CreateAppAsync();
        var id = await app.UploadAndProcessAsync(Folder.RootId, "notes.txt", TestFiles.Text("Revenue grew."));
        var filePath = Path.Combine(app.DocumentsDirectory, $"{id}.txt");
        Assert.True(File.Exists(filePath));

        await app.RunAsync(sp => sp.GetRequiredService<CitadelIQ.Application.Documents.IDocumentService>().DeleteDocumentAsync(id));

        Assert.Equal(0, await app.ScalarAsync<long>("SELECT count(*) FROM \"Documents\""));
        Assert.Equal(0, await app.ScalarAsync<long>("SELECT count(*) FROM \"DocumentChunks\""));
        Assert.False(File.Exists(filePath));
    }
}
