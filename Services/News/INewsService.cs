using apiprojnew.Common;
using apiprojnew.DTO;
using apiprojnew.Models;

namespace apiprojnew.Services.News
{
    public interface INewsService
    {
        Task<Result<List<NewsResponseDto>>> GetAllNewsAsync();
        Task<Result<NewsResponseDto>> CreateNewsAsync(CreateNewsDto dto);
        Task<Result<NewsResponseDto>> UpdateNewsAsync(int id, CreateNewsDto dto);
        Task<Result<string>> DeleteNewsAsync(int id);
        Task<Result<NewsAttachmentDto>> UploadAttachmentAsync(int newsId, IFormFile file);
        Task<Result<string>> DeleteAttachmentAsync(int attachmentId);
        Task<(Stream? Stream, string FileName, string ContentType)?> DownloadAttachmentAsync(int attachmentId);
    }
}
