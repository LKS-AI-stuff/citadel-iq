using Microsoft.AspNetCore.DataProtection;

namespace CitadelIQ.Api.Authentication;

/// <summary>
/// The keys that encrypt the session cookie (and the OIDC correlation/nonce cookies). Without a persistent key ring,
/// every container restart or rebuild silently signs everyone out, and a second API instance can't read the first
/// one's cookies. A fixed application name keeps the ring valid if the deployment path changes.
/// </summary>
public static class DataProtectionSetup
{
    public const string ApplicationName = "CitadelIQ";

    public static void AddCitadelDataProtection(this WebApplicationBuilder builder)
    {
        var dataProtection = builder.Services.AddDataProtection().SetApplicationName(ApplicationName);

        // Unset: ASP.NET's per-user default (~/.aspnet/DataProtection-Keys), fine for local development.
        // Set (Docker): a directory on a persistent volume, relative to the content root unless absolute.
        var keysPath = builder.Configuration["DataProtection:KeysPath"];
        if (!string.IsNullOrWhiteSpace(keysPath))
        {
            var directory = Path.IsPathRooted(keysPath) ? keysPath : Path.Combine(builder.Environment.ContentRootPath, keysPath);
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(directory));
        }
    }
}
