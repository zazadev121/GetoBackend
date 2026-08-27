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
            var roleClaim = User.FindFirst(ClaimTypes.Role);
            return roleClaim?.Value == UserRoles.Admin.ToString();
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
                return Forbid();
            }

            var result = await _newsService.CreateNewsAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteNews(int id)
        {
            if (!IsAdmin())
            {
                return Forbid();
            }

            var result = await _newsService.DeleteNewsAsync(id);
            return StatusCode(result.StatusCode, result);
        }
    }
}
