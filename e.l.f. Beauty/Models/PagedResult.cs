namespace e.l.f._Beauty.Models
{
    public class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; }
        public int TotalItems { get; }
        public int Page { get; }
        public int PageSize { get; }

        public PagedResult(IEnumerable<T> items, int totalItems, int page, int pageSize)
        {
            Items = items?.ToList() ?? new List<T>();// mull safe
            TotalItems = totalItems;
            Page = page;
            PageSize = pageSize;
        }
    }
}