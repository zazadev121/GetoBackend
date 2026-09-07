using apiprojnew.Common;
using apiprojnew.Data;
using apiprojnew.DTO;
using apiprojnew.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace apiprojnew.Services.Vacancies
{
    /// <summary>
    /// Mirrors NewsService: same shape and attachment handling, with its own
    /// upload folder, its own push destination and a salary field.
    /// </summary>
    public class VacancyService : IVacancyService
    {
        private const string UploadFolder = "vacancy-attachments";

        private readonly DataContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly WebPushService _pushService;

        public VacancyService(DataContext context, IWebHostEnvironment env, WebPushService pushService)
        {
            _context = context;
            _env = env;
            _pushService = pushService;
        }

        private static VacancyResponseDto MapToDto(Vacancy v)
        {
            var links = new List<VacancyLinkDto>();
            if (!string.IsNullOrEmpty(v.LinksJson))
            {
                try { links = JsonSerializer.Deserialize<List<VacancyLinkDto>>(v.LinksJson) ?? new(); }
                catch { /* ignore bad json */ }
            }

            return new VacancyResponseDto
            {
                Id = v.Id,
                Title = v.Title,
                Text = v.Text,
                TitleEn = v.TitleEn,
                TextEn = v.TextEn,
                Salary = v.Salary,
                DateCreated = v.DateCreated,
                Links = links,
                Attachments = v.Attachments.Select(a => new VacancyAttachmentDto
                {
                    Id = a.Id,
                    FileName = a.FileName,
                    FileSize = a.FileSize,
                    ContentType = a.ContentType,
                    UploadedAt = a.UploadedAt
                }).ToList()
            };
        }

        public async Task<Result<List<VacancyResponseDto>>> GetAllVacanciesAsync()
        {
            try
            {
                var list = await _context.Vacancies
                    .Include(v => v.Attachments)
                    .OrderByDescending(v => v.DateCreated)
                    .ToListAsync();

                return Result<List<VacancyResponseDto>>.Ok(list.Select(MapToDto).ToList());
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException != null ? $" Inner: {ex.InnerException.Message}" : "";
                return Result<List<VacancyResponseDto>>.BadRequest($"Failed to retrieve vacancies: {ex.Message}{inner}");
            }
        }

        public async Task<Result<VacancyResponseDto>> CreateVacancyAsync(CreateVacancyDto dto)
        {
            if (dto == null) return Result<VacancyResponseDto>.BadRequest("Invalid request payload");
            if (string.IsNullOrWhiteSpace(dto.Title)) return Result<VacancyResponseDto>.BadRequest("Title is required");
            if (string.IsNullOrWhiteSpace(dto.Text)) return Result<VacancyResponseDto>.BadRequest("Text content is required");

            try
            {
                string? linksJson = null;
                if (dto.Links != null && dto.Links.Count > 0)
                    linksJson = JsonSerializer.Serialize(dto.Links);

                var vacancy = new Vacancy
                {
                    Title = dto.Title.Trim(),
                    Text = dto.Text.Trim(),
                    TitleEn = string.IsNullOrWhiteSpace(dto.TitleEn) ? null : dto.TitleEn.Trim(),
                    TextEn = string.IsNullOrWhiteSpace(dto.TextEn) ? null : dto.TextEn.Trim(),
                    Salary = string.IsNullOrWhiteSpace(dto.Salary) ? null : dto.Salary.Trim(),
                    LinksJson = linksJson,
                    DateCreated = DateTime.UtcNow
                };

                _context.Vacancies.Add(vacancy);
                await _context.SaveChangesAsync();

                await _pushService.SendToAllAsync(
                    "💼 ახალი ვაკანსია — GETO Project",
                    vacancy.Title,
                    $"/vacancies/{vacancy.Id}"
                );

                return Result<VacancyResponseDto>.Ok(MapToDto(vacancy));
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException != null ? $" Inner: {ex.InnerException.Message}" : "";
                return Result<VacancyResponseDto>.BadRequest($"Failed to create vacancy: {ex.Message}{inner}");
            }
        }

        public async Task<Result<VacancyResponseDto>> UpdateVacancyAsync(int id, CreateVacancyDto dto)
        {
            if (dto == null) return Result<VacancyResponseDto>.BadRequest("Invalid request payload");
            if (string.IsNullOrWhiteSpace(dto.Title)) return Result<VacancyResponseDto>.BadRequest("Title is required");
            if (string.IsNullOrWhiteSpace(dto.Text)) return Result<VacancyResponseDto>.BadRequest("Text content is required");

            try
            {
                var vacancy = await _context.Vacancies.Include(v => v.Attachments).FirstOrDefaultAsync(v => v.Id == id);
                if (vacancy == null) return Result<VacancyResponseDto>.NotFound("Vacancy not found");

                string? linksJson = null;
                if (dto.Links != null && dto.Links.Count > 0)
                    linksJson = JsonSerializer.Serialize(dto.Links);

                vacancy.Title = dto.Title.Trim();
                vacancy.Text = dto.Text.Trim();
                vacancy.TitleEn = string.IsNullOrWhiteSpace(dto.TitleEn) ? null : dto.TitleEn.Trim();
                vacancy.TextEn = string.IsNullOrWhiteSpace(dto.TextEn) ? null : dto.TextEn.Trim();
                vacancy.Salary = string.IsNullOrWhiteSpace(dto.Salary) ? null : dto.Salary.Trim();
                vacancy.LinksJson = linksJson;

                await _context.SaveChangesAsync();

                return Result<VacancyResponseDto>.Ok(MapToDto(vacancy));
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException != null ? $" Inner: {ex.InnerException.Message}" : "";
                return Result<VacancyResponseDto>.BadRequest($"Failed to update vacancy: {ex.Message}{inner}");
            }
        }

        public async Task<Result<string>> DeleteVacancyAsync(int id)
        {
            try
            {
                var vacancy = await _context.Vacancies.Include(v => v.Attachments).FirstOrDefaultAsync(v => v.Id == id);
                if (vacancy == null) return Result<string>.NotFound("Vacancy not found");

                foreach (var att in vacancy.Attachments)
                    DeleteStoredFile(att.StoredFileName);

                _context.Vacancies.Remove(vacancy);
                await _context.SaveChangesAsync();

                return Result<string>.Ok("Vacancy deleted successfully");
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException != null ? $" Inner: {ex.InnerException.Message}" : "";
                return Result<string>.BadRequest($"Failed to delete vacancy: {ex.Message}{inner}");
            }
        }

        public async Task<Result<VacancyAttachmentDto>> UploadAttachmentAsync(int vacancyId, IFormFile file)
        {
            try
            {
                var vacancy = await _context.Vacancies.FindAsync(vacancyId);
                if (vacancy == null) return Result<VacancyAttachmentDto>.NotFound("Vacancy not found");

                if (file == null || file.Length == 0)
                    return Result<VacancyAttachmentDto>.BadRequest("No file provided");

                const long maxSize = 20 * 1024 * 1024; // 20MB
                if (file.Length > maxSize)
                    return Result<VacancyAttachmentDto>.BadRequest("File size exceeds 20MB limit");

                var uploadsDir = UploadsDir();
                Directory.CreateDirectory(uploadsDir);

                var storedFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                var filePath = Path.Combine(uploadsDir, storedFileName);

                using (var stream = File.Create(filePath))
                    await file.CopyToAsync(stream);

                var attachment = new VacancyAttachment
                {
                    VacancyId = vacancyId,
                    FileName = file.FileName,
                    StoredFileName = storedFileName,
                    FileSize = file.Length,
                    ContentType = file.ContentType,
                    UploadedAt = DateTime.UtcNow
                };

                _context.VacancyAttachments.Add(attachment);
                await _context.SaveChangesAsync();

                return Result<VacancyAttachmentDto>.Ok(new VacancyAttachmentDto
                {
                    Id = attachment.Id,
                    FileName = attachment.FileName,
                    FileSize = attachment.FileSize,
                    ContentType = attachment.ContentType,
                    UploadedAt = attachment.UploadedAt
                });
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException != null ? $" Inner: {ex.InnerException.Message}" : "";
                return Result<VacancyAttachmentDto>.BadRequest($"Failed to upload attachment: {ex.Message}{inner}");
            }
        }

        public async Task<Result<string>> DeleteAttachmentAsync(int attachmentId)
        {
            try
            {
                var attachment = await _context.VacancyAttachments.FindAsync(attachmentId);
                if (attachment == null) return Result<string>.NotFound("Attachment not found");

                DeleteStoredFile(attachment.StoredFileName);

                _context.VacancyAttachments.Remove(attachment);
                await _context.SaveChangesAsync();

                return Result<string>.Ok("Attachment deleted");
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException != null ? $" Inner: {ex.InnerException.Message}" : "";
                return Result<string>.BadRequest($"Failed to delete attachment: {ex.Message}{inner}");
            }
        }

        public async Task<(Stream? Stream, string FileName, string ContentType)?> DownloadAttachmentAsync(int attachmentId)
        {
            var attachment = await _context.VacancyAttachments.FindAsync(attachmentId);
            if (attachment == null) return null;

            var filePath = Path.Combine(UploadsDir(), attachment.StoredFileName);
            if (!File.Exists(filePath)) return null;

            var stream = File.OpenRead(filePath);
            return (stream, attachment.FileName, attachment.ContentType);
        }

        private string UploadsDir() =>
            Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, "Uploads", UploadFolder);

        private void DeleteStoredFile(string storedFileName)
        {
            try
            {
                var filePath = Path.Combine(UploadsDir(), storedFileName);
                if (File.Exists(filePath)) File.Delete(filePath);
            }
            catch { /* best-effort */ }
        }
    }
}
