using apiprojnew.Common;
using apiprojnew.Data;
using apiprojnew.DTO;
using apiprojnew.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace apiprojnew.Services.News
{
    public class NewsService : INewsService
    {
        private readonly DataContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly WebPushService _pushService;

        public NewsService(DataContext context, IWebHostEnvironment env, WebPushService pushService)
        {
            _context = context;
            _env = env;
            _pushService = pushService;
        }

        private static NewsResponseDto MapToDto(Models.News n)
        {
            var links = new List<NewsLinkDto>();
            if (!string.IsNullOrEmpty(n.LinksJson))
            {
                try { links = JsonSerializer.Deserialize<List<NewsLinkDto>>(n.LinksJson) ?? new(); }
                catch { /* ignore bad json */ }
            }

            return new NewsResponseDto
            {
                Id = n.Id,
                Title = n.Title,
                Text = n.Text,
                TitleEn = n.TitleEn,
                TextEn = n.TextEn,
                DateCreated = n.DateCreated,
                Links = links,
                Attachments = n.Attachments.Select(a => new NewsAttachmentDto
                {
                    Id = a.Id,
                    FileName = a.FileName,
                    FileSize = a.FileSize,
                    ContentType = a.ContentType,
                    UploadedAt = a.UploadedAt
                }).ToList()
            };
        }

        public async Task<Result<List<NewsResponseDto>>> GetAllNewsAsync()
        {
            try
            {
                var newsList = await _context.News
                    .Include(n => n.Attachments)
                    .OrderByDescending(n => n.DateCreated)
                    .ToListAsync();

                return Result<List<NewsResponseDto>>.Ok(newsList.Select(MapToDto).ToList());
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException != null ? $" Inner: {ex.InnerException.Message}" : "";
                return Result<List<NewsResponseDto>>.BadRequest($"Failed to retrieve news: {ex.Message}{inner}");
            }
        }

        public async Task<Result<NewsResponseDto>> CreateNewsAsync(CreateNewsDto dto)
        {
            if (dto == null) return Result<NewsResponseDto>.BadRequest("Invalid request payload");
            if (string.IsNullOrWhiteSpace(dto.Title)) return Result<NewsResponseDto>.BadRequest("Title is required");
            if (string.IsNullOrWhiteSpace(dto.Text)) return Result<NewsResponseDto>.BadRequest("Text content is required");

            try
            {
                string? linksJson = null;
                if (dto.Links != null && dto.Links.Count > 0)
                    linksJson = JsonSerializer.Serialize(dto.Links);

                var newsItem = new Models.News
                {
                    Title = dto.Title.Trim(),
                    Text = dto.Text.Trim(),
                    TitleEn = string.IsNullOrWhiteSpace(dto.TitleEn) ? null : dto.TitleEn.Trim(),
                    TextEn = string.IsNullOrWhiteSpace(dto.TextEn) ? null : dto.TextEn.Trim(),
                    LinksJson = linksJson,
                    DateCreated = DateTime.UtcNow
                };

                _context.News.Add(newsItem);
                await _context.SaveChangesAsync();

                // Push notification to all subscribers
                await _pushService.SendToAllAsync(
                    "📰 სიახლე — GETO Project",
                    newsItem.Title,
                    $"/news/{newsItem.Id}"
                );

                return Result<NewsResponseDto>.Ok(MapToDto(newsItem));
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException != null ? $" Inner: {ex.InnerException.Message}" : "";
                return Result<NewsResponseDto>.BadRequest($"Failed to create news item: {ex.Message}{inner}");
            }
        }

        public async Task<Result<NewsResponseDto>> UpdateNewsAsync(int id, CreateNewsDto dto)
        {
            if (dto == null) return Result<NewsResponseDto>.BadRequest("Invalid request payload");
            if (string.IsNullOrWhiteSpace(dto.Title)) return Result<NewsResponseDto>.BadRequest("Title is required");
            if (string.IsNullOrWhiteSpace(dto.Text)) return Result<NewsResponseDto>.BadRequest("Text content is required");

            try
            {
                var newsItem = await _context.News.Include(n => n.Attachments).FirstOrDefaultAsync(n => n.Id == id);
                if (newsItem == null) return Result<NewsResponseDto>.NotFound("News item not found");

                string? linksJson = null;
                if (dto.Links != null && dto.Links.Count > 0)
                    linksJson = JsonSerializer.Serialize(dto.Links);

                newsItem.Title = dto.Title.Trim();
                newsItem.Text = dto.Text.Trim();
                newsItem.TitleEn = string.IsNullOrWhiteSpace(dto.TitleEn) ? null : dto.TitleEn.Trim();
                newsItem.TextEn = string.IsNullOrWhiteSpace(dto.TextEn) ? null : dto.TextEn.Trim();
                newsItem.LinksJson = linksJson;

                await _context.SaveChangesAsync();

                return Result<NewsResponseDto>.Ok(MapToDto(newsItem));
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException != null ? $" Inner: {ex.InnerException.Message}" : "";
                return Result<NewsResponseDto>.BadRequest($"Failed to update news item: {ex.Message}{inner}");
            }
        }

        public async Task<Result<string>> DeleteNewsAsync(int id)
        {
            try
            {
                var newsItem = await _context.News.Include(n => n.Attachments).FirstOrDefaultAsync(n => n.Id == id);
                if (newsItem == null) return Result<string>.NotFound("News item not found");

                // Delete stored files
                foreach (var att in newsItem.Attachments)
                    DeleteStoredFile(att.StoredFileName);

                _context.News.Remove(newsItem);
                await _context.SaveChangesAsync();

                return Result<string>.Ok("News item deleted successfully");
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException != null ? $" Inner: {ex.InnerException.Message}" : "";
                return Result<string>.BadRequest($"Failed to delete news item: {ex.Message}{inner}");
            }
        }

        public async Task<Result<NewsAttachmentDto>> UploadAttachmentAsync(int newsId, IFormFile file)
        {
            try
            {
                var newsItem = await _context.News.FindAsync(newsId);
                if (newsItem == null) return Result<NewsAttachmentDto>.NotFound("News item not found");

                if (file == null || file.Length == 0)
                    return Result<NewsAttachmentDto>.BadRequest("No file provided");

                const long maxSize = 20 * 1024 * 1024; // 20MB
                if (file.Length > maxSize)
                    return Result<NewsAttachmentDto>.BadRequest("File size exceeds 20MB limit");

                var uploadsDir = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, "Uploads", "news-attachments");
                Directory.CreateDirectory(uploadsDir);

                var storedFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                var filePath = Path.Combine(uploadsDir, storedFileName);

                using (var stream = File.Create(filePath))
                    await file.CopyToAsync(stream);

                var attachment = new NewsAttachment
                {
                    NewsId = newsId,
                    FileName = file.FileName,
                    StoredFileName = storedFileName,
                    FileSize = file.Length,
                    ContentType = file.ContentType,
                    UploadedAt = DateTime.UtcNow
                };

                _context.NewsAttachments.Add(attachment);
                await _context.SaveChangesAsync();

                return Result<NewsAttachmentDto>.Ok(new NewsAttachmentDto
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
                return Result<NewsAttachmentDto>.BadRequest($"Failed to upload attachment: {ex.Message}{inner}");
            }
        }

        public async Task<Result<string>> DeleteAttachmentAsync(int attachmentId)
        {
            try
            {
                var attachment = await _context.NewsAttachments.FindAsync(attachmentId);
                if (attachment == null) return Result<string>.NotFound("Attachment not found");

                DeleteStoredFile(attachment.StoredFileName);

                _context.NewsAttachments.Remove(attachment);
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
            var attachment = await _context.NewsAttachments.FindAsync(attachmentId);
            if (attachment == null) return null;

            var uploadsDir = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, "Uploads", "news-attachments");
            var filePath = Path.Combine(uploadsDir, attachment.StoredFileName);

            if (!File.Exists(filePath)) return null;

            var stream = File.OpenRead(filePath);
            return (stream, attachment.FileName, attachment.ContentType);
        }

        private void DeleteStoredFile(string storedFileName)
        {
            try
            {
                var uploadsDir = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, "Uploads", "news-attachments");
                var filePath = Path.Combine(uploadsDir, storedFileName);
                if (File.Exists(filePath)) File.Delete(filePath);
            }
            catch { /* best-effort */ }
        }
    }
}
