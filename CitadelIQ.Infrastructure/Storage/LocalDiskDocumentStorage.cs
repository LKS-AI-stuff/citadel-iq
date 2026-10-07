using CitadelIQ.Application.Interfaces;
using CitadelIQ.Application.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Infrastructure.Storage;

/// <summary>
/// Writes raw uploaded file bytes to a local folder (default: App_Data/documents next to the API
/// project — deliberately not bin/, which dotnet build/clean wipes). See PLAN.md §3.
/// </summary>
public class LocalDiskDocumentStorage(IOptions<StorageOptions> options, IHostEnvironment environment) : IDocumentStorage
{
    // ContentRootPath (not Directory.GetCurrentDirectory()) — it's resolved once at host startup
    // and is overridable via ASPNETCORE_CONTENTROOT for deployment, rather than depending on
    // whatever directory the process happened to be launched from.
    private string RootPath => Path.Combine(environment.ContentRootPath, options.Value.DocumentsPath);

    public async Task SaveAsync(Guid workspaceId, Guid documentId, string fileExtension, Stream content, CancellationToken cancellationToken = default)
    {
        var path = GetFilePath(workspaceId, documentId, fileExtension);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var fileStream = File.Create(path);
        await content.CopyToAsync(fileStream, cancellationToken);
    }

    public Task<Stream> OpenReadAsync(Guid workspaceId, Guid documentId, string fileExtension, CancellationToken cancellationToken = default)
    {
        var path = GetFilePath(workspaceId, documentId, fileExtension);
        Stream stream = File.OpenRead(path);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(Guid workspaceId, Guid documentId, string fileExtension, CancellationToken cancellationToken = default)
    {
        var path = GetFilePath(workspaceId, documentId, fileExtension);
        File.Delete(path);
        return Task.CompletedTask;
    }

    /// <summary><c>{DocumentsPath}/{workspaceId}/{documentId}{extension}</c> — one directory per workspace.</summary>
    private string GetFilePath(Guid workspaceId, Guid documentId, string fileExtension) =>
        Path.Combine(RootPath, workspaceId.ToString(), $"{documentId}{fileExtension}");
}
