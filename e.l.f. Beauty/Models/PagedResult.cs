using e.l.f._Beauty.Models;

namespace e.l.f._Beauty.Services
{
    public class PagedResult<T>
    {
        public IEnumerable<Brewery> items { get; set; }
        private int totalItems;
        private int page;
        private int pageSize;

        public PagedResult(IEnumerable<Brewery> items, int totalItems, int page, int pageSize)
        {
            this.items = items;
            this.totalItems = totalItems;
            this.page = page;
            this.pageSize = pageSize;
        }
    }
}