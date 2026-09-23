using e.l.f._Beauty.Models;

public interface IBreweryFilter
{
    IEnumerable<Brewery> Apply(IEnumerable<Brewery> breweries, BreweryQueryOptions options);
}