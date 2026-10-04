namespace AldaJoyeros.Helpers
{
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
        public int StartItem => TotalItems == 0 ? 0 : (PageNumber - 1) * PageSize + 1;
        public int EndItem => Math.Min(PageNumber * PageSize, TotalItems);

        public static PagedResult<T> Create(IEnumerable<T> source, int pageNumber, int pageSize)
        {
            if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));
            var count = source.Count();
            var totalPages = (int)Math.Ceiling(count / (double)pageSize);
            pageNumber = Math.Clamp(pageNumber, 1, Math.Max(1, totalPages));
            var items = source.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

            return new PagedResult<T>
            {
                Items = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalItems = count
            };
        }
    }
}
