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
        CreateMap<Document, DocumentSummaryDto>();
    }
}
