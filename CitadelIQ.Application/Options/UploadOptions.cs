namespace CitadelIQ.Application.Options;

public class UploadOptions
{
    public int MaxFileSizeMB { get; set; } = 20;

    public string[] AllowedExtensions { get; set; } = [".pdf", ".docx", ".txt", ".csv", ".xlsx"];
}
