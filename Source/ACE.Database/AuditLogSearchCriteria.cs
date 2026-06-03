using System;

namespace ACE.Database
{
    public class AuditLogSearchCriteria
    {
        public string Ip { get; set; }
        public string Account { get; set; }
        public string Character { get; set; }
        public string TransferType { get; set; }
        public string ItemContains { get; set; }
        public int Days { get; set; } = 30;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;

        public void Normalize()
        {
            Ip = Ip?.Trim();
            Account = Account?.Trim();
            Character = Character?.Trim();
            TransferType = TransferType?.Trim();
            ItemContains = ItemContains?.Trim();

            if (Days < 1) Days = 1;
            if (Page < 1) Page = 1;
            if (PageSize < 1) PageSize = 1;
            if (PageSize > 100) PageSize = 100;
        }
    }

    public class PagedResult<T>
    {
        public System.Collections.Generic.List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
    }
}
