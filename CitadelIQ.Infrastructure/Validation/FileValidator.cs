using CitadelIQ.Application.Common;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Application.Options;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Infrastructure.Validation;

public class FileValidator(IOptions<UploadOptions> options) : IFileValidator
{
    public void Validate(string fileName, long sizeBytes)
    {
        if (sizeBytes <= 0)
        {
            throw new ValidationException("This file is empty.");
        }

        var maxBytes = (long)options.Value.MaxFileSizeMB * 1024 * 1024;
        if (sizeBytes > maxBytes)
        {
            throw new ValidationException($"This file exceeds the maximum allowed size of {options.Value.MaxFileSizeMB} MB.");
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!options.Value.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new ValidationException($"Files of type \"{extension}\" are not supported.");
        }
    }
}
