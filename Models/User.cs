using apiprojnew.Enum;
using System.Reflection.Metadata;

namespace apiprojnew.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string LastName { get; set; }
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public UserRoles Role { get; set; }
        public userstatus Status { get; set; }
        public UserPahse UserPahse { get; set; }
        public bool IsVerified { get; set; }
        public string? VerifyCode { get; set; }
        public List<Document> Documents { get; set; } = new List<Document>();
        


    }
}
