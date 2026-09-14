using e.l.f._Beauty.Models;

public class NameSorter : IBrewerySorter
{
    public IEnumerable<Brewery> Sort(IEnumerable<Brewery> breweries, BreweryQueryOptions options)
    {
        return options.Ascending
            ? breweries.OrderBy(b => b.Name ?? string.Empty)
            : breweries.OrderByDescending(b => b.Name ?? string.Empty);
    }
}
