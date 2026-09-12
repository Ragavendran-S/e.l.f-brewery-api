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
        CreateMap<ExternalBrewery, Brewery>();
    }
}
