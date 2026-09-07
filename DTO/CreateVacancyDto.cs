using System.Collections.Generic;

namespace apiprojnew.DTO
{
    public class CreateVacancyDto
    {
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string? TitleEn { get; set; }
        public string? TextEn { get; set; }
        public string? Salary { get; set; }
        public List<VacancyLinkDto>? Links { get; set; }
    }

    public class VacancyLinkDto
    {
        public string Label { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }

    public class VacancyAttachmentDto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
    }

    public class VacancyResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string? TitleEn { get; set; }
        public string? TextEn { get; set; }
        public string? Salary { get; set; }
        public DateTime DateCreated { get; set; }
        public List<VacancyLinkDto> Links { get; set; } = new();
        public List<VacancyAttachmentDto> Attachments { get; set; } = new();
    }
}
