using System;
using System.Collections.Generic;

namespace apiprojnew.Models
{
    public class Vacancy
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string? TitleEn { get; set; }
        public string? TextEn { get; set; }

        /// <summary>Free text, e.g. "€2,400 – 2,800 / month". Optional.</summary>
        public string? Salary { get; set; }

        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        // Stored as JSON array string, e.g. [{"label":"...","url":"..."}]
        public string? LinksJson { get; set; }

        public ICollection<VacancyAttachment> Attachments { get; set; } = new List<VacancyAttachment>();
    }
}
