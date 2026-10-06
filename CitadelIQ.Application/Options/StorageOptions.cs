namespace CitadelIQ.Application.Options;

public class StorageOptions
{
    public const string LocalDiskProvider = "LocalDisk";
    public const string AzureBlobProvider = "AzureBlob";

    /// <summary><c>LocalDisk</c> (default) or <c>AzureBlob</c>.</summary>
    public string Provider { get; set; } = LocalDiskProvider;

    /// <summary>Local provider only. Relative to the API project's working directory — deliberately not under bin/,
    /// which dotnet build/clean wipes and regenerates.</summary>
    public string DocumentsPath { get; set; } = "App_Data/documents";

    public AzureBlobOptions AzureBlob { get; set; } = new();
}

public class AzureBlobOptions
{
    /// <summary>Storage account name; the endpoint is derived as <c>https://{AccountName}.blob.core.windows.net</c>
    /// unless <see cref="ServiceUri"/> is set. Used with Entra ID / managed identity authentication.</summary>
    public string AccountName { get; set; } = string.Empty;

    /// <summary>Private container holding the raw files. Created once by the operator.</summary>
    public string ContainerName { get; set; } = "documents";

    /// <summary>Optional endpoint override (Azurite, sovereign clouds).</summary>
    public string? ServiceUri { get; set; }

    /// <summary>Secret — user-secrets / <c>Storage__AzureBlob__ConnectionString</c> only, never appsettings.json.
    /// When set it takes precedence over Entra ID authentication.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Dev/Azurite convenience; leave false in production so a wrong container name fails loudly.</summary>
    public bool CreateContainerIfMissing { get; set; }
}
