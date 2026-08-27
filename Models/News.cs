using System;
using System.Collections.Generic;

namespace apiprojnew.Models
{
    public class News
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string? TitleEn { get; set; }
        public string? TextEn { get; set; }
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
        // Stored as JSON array string, e.g. [{"label":"...","url":"..."}]
        public string? LinksJson { get; set; }
        public ICollection<NewsAttachment> Attachments { get; set; } = new List<NewsAttachment>();
    }
}
