using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Infrastructure.Storage;

/// <summary>
/// Stores raw uploaded files as blobs named <c>{documentId}{extension}</c> in a private Azure Blob container.
/// Authenticates with the configured connection string if there is one, otherwise with
/// <see cref="DefaultAzureCredential"/> (managed identity / <c>az login</c>) against the account endpoint.
/// The client is created lazily, like the OpenAI clients, so a bad configuration fails only when storage is used.
/// </summary>
public class AzureBlobDocumentStorage(IOptions<StorageOptions> options, ILogger<AzureBlobDocumentStorage> logger)
    : IDocumentStorage, IStorageProbe
{
    private readonly Lazy<BlobContainerClient> _container = new(() => CreateContainerClient(options.Value.AzureBlob));

    public async Task SaveAsync(Guid documentId, string fileExtension, Stream content, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureContainerAsync(cancellationToken);
            await _container.Value.GetBlobClient(BlobName(documentId, fileExtension)).UploadAsync(content, overwrite: true, cancellationToken);
        }
        catch (Exception ex) when (ex is RequestFailedException or AuthenticationFailedException)
        {
            throw Wrap("save", ex);
        }
    }

    public async Task<Stream> OpenReadAsync(Guid documentId, string fileExtension, CancellationToken cancellationToken = default)
    {
        try
        {
            // BlobClient.OpenReadAsync returns a seekable, buffered stream, which the PDF/DOCX/XLSX extractors need.
            return await _container.Value.GetBlobClient(BlobName(documentId, fileExtension)).OpenReadAsync(cancellationToken: cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Same exception the local provider surfaces for a missing file.
            throw new FileNotFoundException("The stored file was not found.", BlobName(documentId, fileExtension), ex);
        }
        catch (Exception ex) when (ex is RequestFailedException or AuthenticationFailedException)
        {
            throw Wrap("read", ex);
        }
    }

    public async Task DeleteAsync(Guid documentId, string fileExtension, CancellationToken cancellationToken = default)
    {
        try
        {
            await _container.Value.GetBlobClient(BlobName(documentId, fileExtension)).DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is RequestFailedException or AuthenticationFailedException)
        {
            throw Wrap("delete", ex);
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        // The credential chain can take a long time to fail off-Azure; don't let a health probe hang on it.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            return (await _container.Value.ExistsAsync(timeout.Token)).Value;
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning("Blob storage availability check failed ({ExceptionType})", ex.GetType().Name);
            return false;
        }
    }

    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (options.Value.AzureBlob.CreateContainerIfMissing)
        {
            await _container.Value.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        }
    }

    private static string BlobName(Guid documentId, string fileExtension) => $"{documentId}{fileExtension}";

    private IOException Wrap(string operation, Exception ex)
    {
        // Status code / error code / exception type only — never URIs, SAS tokens or connection strings.
        if (ex is RequestFailedException rfe)
        {
            logger.LogError("Blob storage {Operation} failed with status {Status} ({ErrorCode})", operation, rfe.Status, rfe.ErrorCode);
        }
        else
        {
            logger.LogError("Blob storage {Operation} failed ({ExceptionType})", operation, ex.GetType().Name);
        }

        return new IOException($"Document storage {operation} failed.", ex);
    }

    private static BlobContainerClient CreateContainerClient(AzureBlobOptions o)
    {
        if (!string.IsNullOrWhiteSpace(o.ConnectionString))
        {
            return new BlobContainerClient(o.ConnectionString, o.ContainerName);
        }

        var serviceUri = !string.IsNullOrWhiteSpace(o.ServiceUri)
            ? new Uri(o.ServiceUri)
            : new Uri($"https://{o.AccountName}.blob.core.windows.net");

        return new BlobServiceClient(serviceUri, new DefaultAzureCredential()).GetBlobContainerClient(o.ContainerName);
    }
}
