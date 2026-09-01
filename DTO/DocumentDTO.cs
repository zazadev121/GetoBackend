using apiprojnew.Enum;

namespace apiprojnew.DTO
{
    public class DocumentDTO
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public int FileSize { get; set; }
        public DateTime UploadedAt { get; set; }
        public UserPahse Phase { get; set; } //
        public bool IsAdminUploaded { get; set; }
    }
}
