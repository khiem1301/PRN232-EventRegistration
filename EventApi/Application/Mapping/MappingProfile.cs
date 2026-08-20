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

        CreateMap<Event, EventDetailDto>()
            .ForMember(d => d.RegisteredCount, opt => opt.Ignore())
            .ForMember(d => d.AvailableSlots, opt => opt.Ignore())
            .ForMember(d => d.Location, opt => opt.MapFrom(s => s.Location))
            .ForMember(d => d.Organizer, opt => opt.MapFrom(s => s.Organizer));

        CreateMap<Location, LocationSummaryDto>();
        CreateMap<Organizer, OrganizerSummaryDto>();
    }
}
