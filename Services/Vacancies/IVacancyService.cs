using apiprojnew.Common;
using apiprojnew.DTO;

namespace apiprojnew.Services.Vacancies
{
    public interface IVacancyService
    {
        Task<Result<List<VacancyResponseDto>>> GetAllVacanciesAsync();
        Task<Result<VacancyResponseDto>> CreateVacancyAsync(CreateVacancyDto dto);
        Task<Result<VacancyResponseDto>> UpdateVacancyAsync(int id, CreateVacancyDto dto);
        Task<Result<string>> DeleteVacancyAsync(int id);
        Task<Result<VacancyAttachmentDto>> UploadAttachmentAsync(int vacancyId, IFormFile file);
        Task<Result<string>> DeleteAttachmentAsync(int attachmentId);
        Task<(Stream? Stream, string FileName, string ContentType)?> DownloadAttachmentAsync(int attachmentId);
    }
}
