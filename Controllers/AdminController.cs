using apiprojnew.Enum;
using apiprojnew.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace apiprojnew.Controllers
{
    [Route("getoProject/[controller]")]
    [ApiController]
    [Authorize]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;

        public AdminController(IAdminService adminService)
        {
            _adminService = adminService;
        }

        private bool IsAdmin()
        {
            var roleClaim = User.FindFirst(ClaimTypes.Role);
            return roleClaim?.Value == UserRoles.Admin.ToString();
        }

        private bool CheckAdminAccess()
        {
            return IsAdmin();
        }

        [HttpPost("document/add-all")]
        public async Task<IActionResult> AddDocumentForAllUsers([FromQuery] int phase, IFormFile file)
        {
            if (!CheckAdminAccess())
            {
                return Forbid();
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest("No file provided");
            }

            byte[] fileData;
            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                fileData = memoryStream.ToArray();
            }

            var phaseEnum = (UserPahse)phase;
            var response = await _adminService.AddDocumentForAllUsersAsync(file.FileName, file.ContentType, fileData, phaseEnum);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost("document/send-to-user/{userId}")]
        public async Task<IActionResult> SendDocumentToUser(int userId, [FromQuery] int phase, [FromQuery] string? note, IFormFile file)
        {
            if (!CheckAdminAccess())
            {
                return Forbid();
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest("No file provided");
            }

            byte[] fileData;
            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                fileData = memoryStream.ToArray();
            }

            var phaseEnum = (UserPahse)phase;
            var response = await _adminService.SendDocumentToSingleUserAsync(userId, file.FileName, file.ContentType, fileData, phaseEnum, note);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsersWithDocuments()
        {
            if (!CheckAdminAccess())
            {
                return Forbid();
            }

            var response = await _adminService.GetAllUsersWithDocumentsAsync();
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("users/search")]
        public async Task<IActionResult> SearchUsers([FromQuery] string name)
        {
            if (!CheckAdminAccess())
            {
                return Forbid();
            }

            var response = await _adminService.SearchUsersByNameAsync(name);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("users/{userId}")]
        public async Task<IActionResult> GetUserWithDocuments(int userId)
        {
            if (!CheckAdminAccess())
            {
                return Forbid();
            }

            var response = await _adminService.GetUserWithDocumentsByIdAsync(userId);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("users/{userId}/documents/{documentId}/download")]
        public async Task<IActionResult> DownloadUserDocument(int userId, int documentId)
        {
            if (!CheckAdminAccess())
            {
                return Forbid();
            }

            var response = await _adminService.DownloadUserDocumentAsync(documentId, userId);

            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }

            // Get document info for file name and content type
            var userResponse = await _adminService.GetUserWithDocumentsByIdAsync(userId);
            var document = userResponse.Data?.Documents.FirstOrDefault(d => d.Id == documentId);

            return File(response.Data, document?.ContentType ?? "application/octet-stream", document?.FileName ?? "document");
        }

        [HttpPut("users/{userId}/status")]
        public async Task<IActionResult> UpdateUserStatus(int userId, [FromQuery] int status, [FromQuery] string? comment)
        {
            if (!CheckAdminAccess())
            {
                return Forbid();
            }

            var statusEnum = (userstatus)status;
            var response = await _adminService.UpdateUserStatusAsync(userId, statusEnum, comment);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPut("users/{userId}/phase")]
        public async Task<IActionResult> UpdateUserPhase(int userId, [FromQuery] int phase, [FromQuery] string? comment)
        {
            if (!CheckAdminAccess())
            {
                return Forbid();
            }

            var phaseEnum = (UserPahse)phase;
            var response = await _adminService.UpdateUserPhaseAsync(userId, phaseEnum, comment);
            return StatusCode(response.StatusCode, response);
        }

        [HttpDelete("users/{userId}")]
        public async Task<IActionResult> DeleteUserWithDocuments(int userId)
        {
            if (!CheckAdminAccess())
            {
                return Forbid();
            }

            var response = await _adminService.DeleteUserWithDocumentsAsync(userId);
            return StatusCode(response.StatusCode, response);
        }

        [HttpDelete("users/{userId}/documents")]
        public async Task<IActionResult> DeleteUserDocumentsOnly(int userId)
        {
            if (!CheckAdminAccess())
            {
                return Forbid();
            }

            var response = await _adminService.DeleteUserDocumentsOnlyAsync(userId);
            return StatusCode(response.StatusCode, response);
        }

        [HttpDelete("documents/{documentId}")]
        public async Task<IActionResult> DeleteSingleDocument(int documentId)
        {
            if (!CheckAdminAccess())
            {
                return Forbid();
            }

            var response = await _adminService.DeleteDocumentByIdAsync(documentId);
            return StatusCode(response.StatusCode, response);
        }

        [HttpDelete("documents/bulk")]
        public async Task<IActionResult> DeleteBulkDocumentsByFileName([FromQuery] string fileName)
        {
            if (!CheckAdminAccess())
            {
                return Forbid();
            }

            var response = await _adminService.DeleteBulkDocumentsByFileNameAsync(fileName);
            return StatusCode(response.StatusCode, response);
        }
    }
}
