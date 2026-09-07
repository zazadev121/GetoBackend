using apiprojnew.Common;
using apiprojnew.DTO;
using apiprojnew.Enum;
using apiprojnew.Services.Vacancies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace apiprojnew.Controllers
{
    [Route("getoProject/[controller]")]
    [ApiController]
    public class VacancyController : ControllerBase
    {
        private readonly IVacancyService _vacancyService;

        public VacancyController(IVacancyService vacancyService)
        {
            _vacancyService = vacancyService;
        }

        private bool IsAdmin()
        {
            var roleClaim = User.FindFirst(ClaimTypes.Role) ?? User.FindFirst("role") ?? User.FindFirst("Role");
            if (roleClaim == null) return false;
            var val = roleClaim.Value;
            return val == UserRoles.Admin.ToString() || val == ((int)UserRoles.Admin).ToString();
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllVacancies()
        {
            var result = await _vacancyService.GetAllVacanciesAsync();
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateVacancy([FromBody] CreateVacancyDto dto)
        {
            if (!IsAdmin())
                return StatusCode(403, Result<VacancyResponseDto>.BadRequest("Admin privileges required to post vacancies"));

            if (dto == null)
                return BadRequest(Result<VacancyResponseDto>.BadRequest("Payload is required"));

            var result = await _vacancyService.CreateVacancyAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> UpdateVacancy(int id, [FromBody] CreateVacancyDto dto)
        {
            if (!IsAdmin())
                return StatusCode(403, Result<VacancyResponseDto>.BadRequest("Admin privileges required to update vacancies"));

            if (dto == null)
                return BadRequest(Result<VacancyResponseDto>.BadRequest("Payload is required"));

            var result = await _vacancyService.UpdateVacancyAsync(id, dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteVacancy(int id)
        {
            if (!IsAdmin())
                return StatusCode(403, Result<string>.BadRequest("Admin privileges required to delete vacancies"));

            var result = await _vacancyService.DeleteVacancyAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{vacancyId}/attachments")]
        [Authorize]
        public async Task<IActionResult> UploadAttachment(int vacancyId, IFormFile file)
        {
            if (!IsAdmin())
                return StatusCode(403, Result<VacancyAttachmentDto>.BadRequest("Admin privileges required to upload vacancy attachments"));

            var result = await _vacancyService.UploadAttachmentAsync(vacancyId, file);
            return StatusCode(result.StatusCode, result);
        }

        [HttpDelete("attachments/{attachmentId}")]
        [Authorize]
        public async Task<IActionResult> DeleteAttachment(int attachmentId)
        {
            if (!IsAdmin())
                return StatusCode(403, Result<string>.BadRequest("Admin privileges required to delete vacancy attachments"));

            var result = await _vacancyService.DeleteAttachmentAsync(attachmentId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("attachments/{attachmentId}/download")]
        [AllowAnonymous]
        public async Task<IActionResult> DownloadAttachment(int attachmentId)
        {
            var res = await _vacancyService.DownloadAttachmentAsync(attachmentId);
            if (res == null || res.Value.Stream == null)
                return NotFound("Attachment not found");

            return File(res.Value.Stream, res.Value.ContentType, res.Value.FileName);
        }
    }
}
