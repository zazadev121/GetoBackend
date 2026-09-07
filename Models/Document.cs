using apiprojnew.Enum;

namespace apiprojnew.Models
{
    public class Document
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; }      
        public byte[] FileData { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }  // fix casing
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public UserPahse Phase { get; set; }  
        public bool IsAdminUploaded { get; set; } = false;
    }
}