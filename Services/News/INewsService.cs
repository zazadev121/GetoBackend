using apiprojnew.Common;
using apiprojnew.DTO;
using apiprojnew.Models;

namespace apiprojnew.Services.News
{
    public interface INewsService
    {
        Task<Result<List<Models.News>>> GetAllNewsAsync();
        Task<Result<Models.News>> CreateNewsAsync(CreateNewsDto dto);
        Task<Result<Models.News>> UpdateNewsAsync(int id, CreateNewsDto dto);
        Task<Result<string>> DeleteNewsAsync(int id);
    }
}
