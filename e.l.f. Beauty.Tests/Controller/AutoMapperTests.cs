using AutoMapper;
using Xunit;

public class AutoMapperTests
{
    private readonly IMapper _mapper;

    public AutoMapperTests()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<BreweryProfile>();
            cfg.AddProfile<CustomerProfile>();
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
        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<Brewery, BreweryDto>();
            cfg.CreateMap<BreweryDto, Brewery>();
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
