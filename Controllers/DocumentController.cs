using apiprojnew.Services.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace apiprojnew.Controllers
{
    [Route("getoProject/[controller]")]
    [ApiController]
    [Authorize]
    public class DocumentController : ControllerBase
    {
        private readonly IDocumentService _documentService;

        public DocumentController(IDocumentService documentService)
        {
            _documentService = documentService;
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId) ? userId : 0;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadDocument(IFormFile file)
        {
            var userId = GetUserId();
            if (userId == 0)
            {
                return Unauthorized("User not authenticated");
            }

            var response = await _documentService.UploadDocumentAsync(userId, file);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetUserDocuments()
        {
            var userId = GetUserId();
            if (userId == 0)
            {
                return Unauthorized("User not authenticated");
            }

            var response = await _documentService.GetUserDocumentsAsync(userId);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("download/{documentId}")]
        public async Task<IActionResult> DownloadDocument(int documentId)
        {
            var userId = GetUserId();
            if (userId == 0)
            {
                return Unauthorized("User not authenticated");
            }

            var response = await _documentService.GetDocumentAsync(documentId, userId);

            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }

            var document = (await _documentService.GetUserDocumentsAsync(userId)).Data
                .FirstOrDefault(d => d.Id == documentId);

            return File(response.Data, document?.ContentType ?? "application/octet-stream", document?.FileName ?? "document");
        }

        [HttpDelete("delete/{documentId}")]
        public async Task<IActionResult> DeleteDocument(int documentId)
        {
            var userId = GetUserId();
            if (userId == 0)
            {
                return Unauthorized("User not authenticated");
            }

            var response = await _documentService.DeleteDocumentAsync(documentId, userId);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("{documentId}")]
        public async Task<IActionResult> GetDocumentById(int documentId)
        {
            var userId = GetUserId();
            if (userId == 0)
            {
                return Unauthorized("User not authenticated");
            }

            var response = await _documentService.GetDocumentByIdAsync(documentId, userId);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("phase/visible")]
        public async Task<IActionResult> GetPhaseVisibleDocuments()
        {
            var userId = GetUserId();
            if (userId == 0)
            {
                return Unauthorized("User not authenticated");
            }

            var response = await _documentService.GetUserVisibleDocumentsAsync(userId);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("admin-phase/list")]
        public async Task<IActionResult> GetAdminDocumentsByPhase()
        {
            var userId = GetUserId();
            if (userId == 0)
            {
                return Unauthorized("User not authenticated");
            }

            var response = await _documentService.GetAdminDocumentsByUserPhaseAsync(userId);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("admin-phase/download/{documentId}")]
        public async Task<IActionResult> DownloadPhaseVisibleDocument(int documentId)
        {
            var userId = GetUserId();
            if (userId == 0)
            {
                return Unauthorized("User not authenticated");
            }

            var response = await _documentService.GetAdminDocumentAsync(documentId, userId);

            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response);
            }

            var documentsResult = await _documentService.GetAdminDocumentsByUserPhaseAsync(userId);
            var document = documentsResult.Data?.FirstOrDefault(d => d.Id == documentId);

            return File(response.Data, document?.ContentType ?? "application/octet-stream", document?.FileName ?? "document");
        }

        [HttpGet("direct-download/{documentId}")]
        [AllowAnonymous]
        public async Task<IActionResult> DirectDownload(int documentId)
        {
            var response = await _documentService.GetDocumentDirectAsync(documentId);
            if (response.StatusCode != 200)
            {
                return StatusCode(response.StatusCode, response.Message);
            }

            return File(response.Data.data, response.Data.contentType ?? "application/octet-stream", response.Data.fileName ?? "document");
        }
    }
}
