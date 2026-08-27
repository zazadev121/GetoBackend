using apiprojnew.Common;
using apiprojnew.DTO;
using apiprojnew.Enum;
using apiprojnew.Services.News;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace apiprojnew.Controllers
{
    [Route("getoProject/[controller]")]
    [ApiController]
    public class NewsController : ControllerBase
    {
        private readonly INewsService _newsService;

        public NewsController(INewsService newsService)
        {
            _newsService = newsService;
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
        public async Task<IActionResult> GetAllNews()
        {
            var result = await _newsService.GetAllNewsAsync();
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateNews([FromBody] CreateNewsDto dto)
        {
            if (!IsAdmin())
            {
                return StatusCode(403, Result<NewsResponseDto>.BadRequest("Admin privileges required to post news"));
            }

            if (dto == null)
            {
                return BadRequest(Result<NewsResponseDto>.BadRequest("Payload is required"));
            }

            var result = await _newsService.CreateNewsAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> UpdateNews(int id, [FromBody] CreateNewsDto dto)
        {
            if (!IsAdmin())
            {
                return StatusCode(403, Result<NewsResponseDto>.BadRequest("Admin privileges required to update news"));
            }

            if (dto == null)
            {
                return BadRequest(Result<NewsResponseDto>.BadRequest("Payload is required"));
            }

            var result = await _newsService.UpdateNewsAsync(id, dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteNews(int id)
        {
            if (!IsAdmin())
            {
                return StatusCode(403, Result<string>.BadRequest("Admin privileges required to delete news"));
            }

            var result = await _newsService.DeleteNewsAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("{newsId}/attachments")]
        [Authorize]
        public async Task<IActionResult> UploadAttachment(int newsId, IFormFile file)
        {
            if (!IsAdmin())
            {
                return StatusCode(403, Result<NewsAttachmentDto>.BadRequest("Admin privileges required to upload news attachments"));
            }

            var result = await _newsService.UploadAttachmentAsync(newsId, file);
            return StatusCode(result.StatusCode, result);
        }

        [HttpDelete("attachments/{attachmentId}")]
        [Authorize]
        public async Task<IActionResult> DeleteAttachment(int attachmentId)
        {
            if (!IsAdmin())
            {
                return StatusCode(403, Result<string>.BadRequest("Admin privileges required to delete news attachments"));
            }

            var result = await _newsService.DeleteAttachmentAsync(attachmentId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("attachments/{attachmentId}/download")]
        [AllowAnonymous]
        public async Task<IActionResult> DownloadAttachment(int attachmentId)
        {
            var res = await _newsService.DownloadAttachmentAsync(attachmentId);
            if (res == null || res.Value.Stream == null)
            {
                return NotFound("Attachment not found");
            }

            return File(res.Value.Stream, res.Value.ContentType, res.Value.FileName);
        }
    }
}
