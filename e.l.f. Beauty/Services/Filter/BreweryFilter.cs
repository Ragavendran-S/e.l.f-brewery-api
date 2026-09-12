using e.l.f._Beauty.Models;

public class BreweryFilter : IBreweryFilter
{
    public IEnumerable<Brewery> Apply(IEnumerable<Brewery> breweries, BreweryQueryOptions options)
    {
        if (!string.IsNullOrEmpty(options.Search))
            breweries = breweries.Where(b => b.Name?.Contains(options.Search, StringComparison.OrdinalIgnoreCase) == true);

        if (!string.IsNullOrEmpty(options.City))
            breweries = breweries.Where(b => b.City?.Equals(options.City, StringComparison.OrdinalIgnoreCase) == true);

        return breweries;
    }
}