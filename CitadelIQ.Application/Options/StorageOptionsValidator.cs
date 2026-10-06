using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Application.Options;

public partial class StorageOptionsValidator : IValidateOptions<StorageOptions>
{
    // Azure container names: 3-63 chars, lowercase letters/digits/hyphens, start and end with a letter or digit, no "--".
    [GeneratedRegex("^[a-z0-9](?:[a-z0-9]|-(?!-)){1,61}[a-z0-9]$")]
    private static partial Regex ContainerName();

    [GeneratedRegex("^[a-z0-9]{3,24}$")]
    private static partial Regex AccountName();

    public ValidateOptionsResult Validate(string? name, StorageOptions o)
    {
        var errors = new List<string>();

        switch (o.Provider)
        {
            case StorageOptions.LocalDiskProvider:
                if (string.IsNullOrWhiteSpace(o.DocumentsPath)) errors.Add("Storage:DocumentsPath must be set for the LocalDisk provider.");
                break;

            case StorageOptions.AzureBlobProvider:
                var azure = o.AzureBlob;
                if (!ContainerName().IsMatch(azure.ContainerName ?? string.Empty))
                    errors.Add("Storage:AzureBlob:ContainerName must be 3-63 characters: lowercase letters, digits and single hyphens.");
                if (string.IsNullOrWhiteSpace(azure.ConnectionString) && string.IsNullOrWhiteSpace(azure.AccountName) && string.IsNullOrWhiteSpace(azure.ServiceUri))
                    errors.Add("Storage:AzureBlob needs an AccountName (Entra ID auth) or a ConnectionString.");
                if (!string.IsNullOrWhiteSpace(azure.AccountName) && !AccountName().IsMatch(azure.AccountName))
                    errors.Add("Storage:AzureBlob:AccountName must be 3-24 characters: lowercase letters and digits only.");
                if (!string.IsNullOrWhiteSpace(azure.ServiceUri) && !Uri.TryCreate(azure.ServiceUri, UriKind.Absolute, out _))
                    errors.Add("Storage:AzureBlob:ServiceUri must be an absolute URI.");
                break;

            default:
                errors.Add($"Storage:Provider '{o.Provider}' is not supported. Use '{StorageOptions.LocalDiskProvider}' or '{StorageOptions.AzureBlobProvider}'.");
                break;
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
