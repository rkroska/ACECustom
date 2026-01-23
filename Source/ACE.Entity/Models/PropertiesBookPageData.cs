using System;

namespace ACE.Entity.Models
{
    public class PropertiesBookPageData
    {
        public required uint AuthorId { get; set; }
        public required string AuthorName { get; set; }
        public required string AuthorAccount { get; set; }
        public required bool IgnoreAuthor { get; set; }
        public required string PageText { get; set; }
    }
}
