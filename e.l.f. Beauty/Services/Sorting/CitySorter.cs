using e.l.f._Beauty.Models;

public class CitySorter : IBrewerySorter
{
    public IEnumerable<Brewery> Sort(IEnumerable<Brewery> breweries, BreweryQueryOptions options)
    {
        return options.Ascending
            ? breweries.OrderBy(b => b.City ?? string.Empty)
            : breweries.OrderByDescending(b => b.City ?? string.Empty);
    }
}
