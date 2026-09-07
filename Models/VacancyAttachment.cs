using System;

namespace apiprojnew.Models
{
    public class VacancyAttachment
    {
        public int Id { get; set; }
        public int VacancyId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string StoredFileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public Vacancy? Vacancy { get; set; }
    }
}
