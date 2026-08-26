using apiprojnew.Common;
using apiprojnew.DTO;

namespace apiprojnew.Services.Users
{
    public interface IUserService
    {
        public Result<int> Register(RegisterDTO req);
        public Result<string> Login(LoginDTO req);
        public Result<string> VerifyEmail(EmailVerifyDTO req);
        public Result<string> ResetPassword(ResetPasswordDTO req);
        public Result<string> ForgotPassword(string Email);
        public Result<UserWithDocumentsDTO> GetProfile(int userId);
    }
}
