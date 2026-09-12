using e.l.f._Beauty.Models;
using e.l.f._Beauty.Services;

public interface IPagingHelper
{
    PagedResult<Brewery> Apply(IEnumerable<Brewery> breweries, BreweryQueryOptions options);
}