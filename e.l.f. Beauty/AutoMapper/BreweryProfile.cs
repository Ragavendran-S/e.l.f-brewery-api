using AutoMapper;
using e.l.f._Beauty.Models;

public class BreweryProfile : Profile
{
    public BreweryProfile()
    {
        CreateMap<ExternalBrewery, BreweryResponse>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.brewery_id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.brewery_name))
            .ForMember(dest => dest.City, opt => opt.MapFrom(src => src.location_city))
            .ForMember(dest => dest.State, opt => opt.MapFrom(src => src.location_state))
            .ForMember(dest => dest.Country, opt => opt.MapFrom(src => src.location_country));
        CreateMap<ExternalBrewery, Brewery>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.brewery_id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.brewery_name))
            .ForMember(dest => dest.City, opt => opt.MapFrom(src => src.location_city))
            .ForMember(dest => dest.State, opt => opt.MapFrom(src => src.location_state))
            .ForMember(dest => dest.Country, opt => opt.MapFrom(src => src.location_country))
            // properties that don't exist on ExternalBrewery should be ignored for mapping validation
            .ForMember(dest => dest.Brewery_Type, opt => opt.Ignore())
            .ForMember(dest => dest.Street, opt => opt.Ignore())
            .ForMember(dest => dest.Phone, opt => opt.Ignore())
            .ForMember(dest => dest.Latitude, opt => opt.Ignore())
            .ForMember(dest => dest.Longitude, opt => opt.Ignore());
    }
}
