using AutoMapper;
using Xunit;
using e.l.f._Beauty.Models;

public class AutoMapperTests
{
    private readonly IMapper _mapper;

    public AutoMapperTests()
    {
        var config = AutoMapperTestHelper.CreateConfiguration(cfg =>
        {
            cfg.AddProfile<BreweryProfile>();
            // CustomerProfile not present in this solution; skip to keep tests focused on brewery mapping
        });

        IMapper mapper = config.CreateMapper();

        //  Validate configuration
        config.AssertConfigurationIsValid();

        _mapper = config.CreateMapper();
    }

    [Fact]
    public void AutoMapper_Configuration_IsValid()
    {
        // If configuration is invalid, this test fails
        var config = AutoMapperTestHelper.CreateConfiguration(cfg =>
        {
            // Simple identity mapping assertions for Brewery <-> BreweryResponse
            cfg.CreateMap<Brewery, BreweryResponse>();
            cfg.CreateMap<BreweryResponse, Brewery>()
               .ForMember(dest => dest.Brewery_Type, opt => opt.Ignore())
               .ForMember(dest => dest.Street, opt => opt.Ignore())
               .ForMember(dest => dest.Phone, opt => opt.Ignore())
               .ForMember(dest => dest.Latitude, opt => opt.Ignore())
               .ForMember(dest => dest.Longitude, opt => opt.Ignore());
        });

        IMapper mapper = config.CreateMapper();


        config.AssertConfigurationIsValid();
    }

    [Fact]
    public void AutoMapper_Maps_External_To_Internal()
    {
        var external = new ExternalBrewery
        {
            brewery_id = "123",
            brewery_name = "Lagunitas Brewing Co",
            location_city = "Petaluma",
            location_state = "California",
            location_country = "USA"
        };

        var result = _mapper.Map<BreweryResponse>(external);

        Assert.Equal("123", result.Id);
        Assert.Equal("Lagunitas Brewing Co", result.Name);
        Assert.Equal("Petaluma", result.City);
        Assert.Equal("California", result.State);
        Assert.Equal("USA", result.Country);
    }
}
