using EventApi.Application.DTOs;
using EventApi.Domain.Entities;
using EventApi.Domain.Enums;

namespace EventApi.Application.Mapping;

public class MappingProfile : AutoMapper.Profile
{
    public MappingProfile()
    {
        CreateMap<User, UserDto>()
            .ForMember(d => d.Role, opt => opt.MapFrom(s => s.Role.ToString()));

        CreateMap<Location, LocationDto>();
        CreateMap<Organizer, OrganizerDto>();
    }
}
