using apiprojnew.Enum;

namespace apiprojnew.DTO
{
    public class UserWithDocumentsDTO
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public UserRoles Role { get; set; }
        public userstatus Status { get; set; }
        public UserPahse UserPhase { get; set; }
        public bool IsVerified { get; set; }
        public List<DocumentDTO> Documents { get; set; } = new List<DocumentDTO>();
    }
}
