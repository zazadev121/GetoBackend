namespace apiprojnew.DTO
{
    public class NewsAttachmentDto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
    }

    public class NewsLinkDto
    {
        public string Label { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }

    public class NewsResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string? TitleEn { get; set; }
        public string? TextEn { get; set; }
        public DateTime DateCreated { get; set; }
        public List<NewsLinkDto> Links { get; set; } = new();
        public List<NewsAttachmentDto> Attachments { get; set; } = new();
    }
}
