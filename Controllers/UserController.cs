using apiprojnew.DTO;
using apiprojnew.Services.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace apiprojnew.Controllers
{
    [Route("getoProject/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserService _UserInterface;
        public UserController(IUserService UserInterface)
        {
            _UserInterface = UserInterface;
        }
        [HttpPost("Register")]
        public IActionResult Register(RegisterDTO request)
        {
            var response = _UserInterface.Register(request);
            return StatusCode(response.StatusCode, response);
        }
        [HttpPost("Login")]
        public IActionResult Login(LoginDTO request)
        {
            var response = _UserInterface.Login(request);
            return StatusCode(response.StatusCode, response);
        }
        [HttpPost("VerifyEmail")]
        public IActionResult VerifyEmail(EmailVerifyDTO request)
        {
            var response = _UserInterface.VerifyEmail(request);
            return StatusCode(response.StatusCode, response);
        }
        [HttpPost("ForgotPassword/{Email}")]
        public IActionResult ForgotPassword(string Email)

        {
            var response = _UserInterface.ForgotPassword(Email);
            return StatusCode(response.StatusCode, response);
        }
        [HttpPost("ResetPassword")]
        public IActionResult ResetPassword(ResetPasswordDTO req)
        {
            var response = _UserInterface.ResetPassword(req);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("me")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public IActionResult GetMyProfile()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User not authenticated");
            }
            var response = _UserInterface.GetProfile(userId);
            return StatusCode(response.StatusCode, response);
        }
    }
}
