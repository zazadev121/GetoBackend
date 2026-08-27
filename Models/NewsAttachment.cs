using System;

namespace apiprojnew.Models
{
    public class NewsAttachment
    {
        public int Id { get; set; }
        public int NewsId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string StoredFileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public News? News { get; set; }
    }
}
