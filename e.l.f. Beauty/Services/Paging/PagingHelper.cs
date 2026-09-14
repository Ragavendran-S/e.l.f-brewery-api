using e.l.f._Beauty.Models;
using e.l.f._Beauty.Services;

public class PagingHelper : IPagingHelper
{
    public PagedResult<Brewery> Apply(IEnumerable<Brewery> breweries, BreweryQueryOptions options)
    {
        var totalItems = breweries.Count();
        var items = breweries.Skip((options.Page - 1) * options.PageSize).Take(options.PageSize).ToList();
        return new PagedResult<Brewery>(items, totalItems, options.Page, options.PageSize);
    }
}