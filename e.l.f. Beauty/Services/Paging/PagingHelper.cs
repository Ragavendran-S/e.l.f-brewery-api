using e.l.f._Beauty.Models;
using System.Linq;

namespace e.l.f._Beauty.Services
{
    // PagingHelper provides paging utilities used by the service and repository layers.

    public class PagingHelper : IPagingHelper
    {
        public PagedResult<Brewery> Apply(IEnumerable<Brewery> breweries, BreweryQueryOptions options)
        {
            var totalItems = breweries.Count();
            var items = breweries.Skip((options.Page - 1) * options.PageSize).Take(options.PageSize).ToList();
            return new PagedResult<Brewery>(items, totalItems, options.Page, options.PageSize);
        }

        public IQueryable<T> ApplyPaging<T>(IQueryable<T> query, int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            return query.Skip((page - 1) * pageSize).Take(pageSize);
        }
    }
}
