namespace CitadelIQ.Application.Interfaces;

/// <summary>
/// Validates an upload against configuration (allowed extensions, max size) before any processing
/// is attempted. Throws <see cref="Common.ValidationException"/> with a safe, user-facing message.
/// </summary>
public interface IFileValidator
{
    void Validate(string fileName, long sizeBytes);
}
