using e.l.f._Beauty.Models;

public interface IBrewerySorter
{
    IEnumerable<Brewery> Sort(IEnumerable<Brewery> breweries, BreweryQueryOptions options);
}
public interface IBrewerySorterFactory
{
    IBrewerySorter GetSorter(string sortBy);
}