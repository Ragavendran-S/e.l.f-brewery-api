using e.l.f._Beauty.Models;

namespace e.l.f._Beauty.Services
{
    public interface IPagingHelper
    {
        PagedResult<Brewery> Apply(IEnumerable<Brewery> breweries, BreweryQueryOptions options);
        IQueryable<T> ApplyPaging<T>(IQueryable<T> query, int page, int pageSize);
    }
}
