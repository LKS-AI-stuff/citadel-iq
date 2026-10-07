namespace CitadelIQ.Application.Dtos;

/// <summary>Who uploaded a document. <see cref="IsFormerMember"/> once they have left or been removed.</summary>
public record UploaderDto(string DisplayName, bool IsFormerMember);
