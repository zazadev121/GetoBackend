using System.Collections.Generic;

namespace apiprojnew.DTO
{
    public class CreateNewsDto
    {
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string? TitleEn { get; set; }
        public string? TextEn { get; set; }
        public List<NewsLinkDto>? Links { get; set; }
    }
}
