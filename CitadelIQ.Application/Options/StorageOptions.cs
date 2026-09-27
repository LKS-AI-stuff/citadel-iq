namespace CitadelIQ.Application.Options;

public class StorageOptions
{
    /// <summary>Relative to the API project's working directory — deliberately not under bin/, which
    /// dotnet build/clean wipes and regenerates.</summary>
    public string DocumentsPath { get; set; } = "App_Data/documents";
}
