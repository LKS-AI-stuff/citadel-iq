using AutoMapper;
using CitadelIQ.Application.Dtos;
using CitadelIQ.Domain.Entities;

namespace CitadelIQ.Application.Mapping;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Folder, FolderDto>();
        CreateMap<Folder, FolderPathSegmentDto>();
        // UploadedBy needs a user lookup; services fill it in with `with { UploadedBy = ... }`.
        CreateMap<Document, DocumentSummaryDto>()
            .ForCtorParam(nameof(DocumentSummaryDto.UploadedBy), o => o.MapFrom(_ => (UploaderDto?)null));
        CreateMap<Document, DocumentStatusDto>();
    }
}
