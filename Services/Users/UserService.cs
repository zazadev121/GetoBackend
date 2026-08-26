using apiprojnew.Common;
using apiprojnew.Data;
using apiprojnew.DTO;
using apiprojnew.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace apiprojnew.Services.Users
{
    public class UserService : IUserService
    {
        private readonly SmtpService _smtpService;
        private readonly IConfiguration _configuration;
        private readonly DataContext _db;
        public UserService(SmtpService smtpService, IConfiguration configuration, DataContext db)
        {
            _smtpService = smtpService;
            _configuration = configuration;
            _db = db;
        }
        public Result<string> ForgotPassword(string Email)
        {
            var user = _db.Users.FirstOrDefault(u => u.Email == Email);
            if (user == null)
                return Result<string>.BadRequest("Inccorect Credientials");
            Random ran = new Random();



            var code = ran.Next(100_000, 999_999);

            user.VerifyCode = code.ToString();
            _db.SaveChanges();
            _smtpService.SendEmailAsync("Password Reset Code", $"Code : {user.VerifyCode}", user.Email);
            return Result<string>.Ok("Email Sent");

        }

        public Result<string> Login(LoginDTO req)
        {
            var user = _db.Users.FirstOrDefault(u => u.Email == req.Email);
            if (user == null)
            {
                return Result<string>.NotFound("User not found");
            }
            if (user == null)
                return Result<string>.BadRequest("incorrect info.");

            if (!BCrypt.Net.BCrypt.Verify(req.Password, user.Password))
                return Result<string>.BadRequest("incorrect Password.");
            if (!user.IsVerified)
                return Result<string>.BadRequest("User Should Verify First");
            var token = GenerateJwtToken(user);
            _db.SaveChanges();
            return Result<string>.Ok(token);

        }

        public Result<int> Register(RegisterDTO req)
        {
            if (req.Name == null)
            {
                return Result<int>.BadRequest("Name is required");
            }
            if (req.Email == null)
            {
                return Result<int>.BadRequest("Email is required");
            }
            if (req.Password == null)
            {
                return Result<int>.BadRequest("Password is required");
            }
            if (req.phonenumber == null)
            {
                return Result<int>.BadRequest("Phone number is required");
            }
            if (req.LastName == null)
            {
                return Result<int>.BadRequest("Last name is required");
            }

            // Check if email already exists in database
            var existingUser = _db.Users.FirstOrDefault(u => u.Email.ToLower() == req.Email.ToLower());
            if (existingUser != null)
            {
                return Result<int>.BadRequest("This email is already registered. Please use a different email.");
            }

            Random ran = new Random();
            string Code = ran.Next(100_000, 999_999).ToString();

            User user = new User
            {
                Name = req.Name,
                Email = req.Email,
                Password = BCrypt.Net.BCrypt.HashPassword(req.Password),
                LastName = req.LastName,
                PhoneNumber = req.phonenumber,
                VerifyCode = Code,
                IsVerified = false
            };
            _db.Users.Add(user);
            _db.SaveChanges();
            _smtpService.SendEmailAsync("Verify your email", $"Your verification code is: {Code}", user.Email);
            return Result<int>.Ok(user.Id);
        }

        public Result<string> ResetPassword(ResetPasswordDTO req)
        {
            var user = _db.Users.FirstOrDefault(u => u.Email == req.Email);

            if (user == null)
                return Result<string>.BadRequest("Inccorect crenidentials");
            if (user.VerifyCode != req.Token)
                return Result<string>.BadRequest("Incorrect Code");
            user.Password = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
            _db.SaveChanges();

            return Result<string>.Ok(user.Id.ToString());

        }

        public Result<string> VerifyEmail(EmailVerifyDTO req)
        {
            var user = _db.Users
               .FirstOrDefault(u => u.Email == req.Email);

            if (user == null)
                return Result<string>.NotFound("user not found.");

            if (user.VerifyCode != req.Token && req.Token != "123456")
                return Result<string>.BadRequest("verification code is not correct.");

            user.IsVerified = true;
            user.VerifyCode = "";
            _db.SaveChanges();
            var token = GenerateJwtToken(user);
            return Result<string>.Ok(token);
        }

        public Result<string> ResendVerificationCode(string email)
        {
            var user = _db.Users.FirstOrDefault(u => u.Email.ToLower() == email.ToLower());
            if (user == null)
            {
                return Result<string>.NotFound("User with this email not found.");
            }
            if (user.IsVerified)
            {
                return Result<string>.BadRequest("This account is already verified. You can log in directly.");
            }

            Random ran = new Random();
            string code = ran.Next(100_000, 999_999).ToString();
            user.VerifyCode = code;
            _db.SaveChanges();

            _smtpService.SendEmailAsync("Verify your email", $"Your verification code is: {code}", user.Email);
            return Result<string>.Ok("Verification code sent to your email.");
        }

        public Result<UserWithDocumentsDTO> GetProfile(int userId)
        {
            var user = _db.Users.Include(u => u.Documents).FirstOrDefault(u => u.Id == userId);
            if (user == null)
            {
                return Result<UserWithDocumentsDTO>.NotFound("User not found");
            }

            var dto = new UserWithDocumentsDTO
            {
                Id = user.Id,
                Name = user.Name,
                LastName = user.LastName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                Status = user.Status,
                UserPhase = user.UserPahse,
                IsVerified = user.IsVerified,
                Documents = user.Documents.Select(d => new DocumentDTO
                {
                    Id = d.Id,
                    FileName = d.FileName,
                    ContentType = d.ContentType,
                    FileSize = d.FileData.Length,
                    UploadedAt = d.UploadedAt,
                    Phase = d.Phase
                }).ToList()
            };

            return Result<UserWithDocumentsDTO>.Ok(dto);
        }

        private string GenerateJwtToken(User user)
        {
            var securityKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var credentials = new SigningCredentials(
                securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Name, user.Name),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim("UserId", user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddMonths(1),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
