using apiprojnew.Common;
using apiprojnew.Data;
using apiprojnew.DTO;
using apiprojnew.Models;
using Microsoft.EntityFrameworkCore;

namespace apiprojnew.Services.News
{
    public class NewsService : INewsService
    {
        private readonly DataContext _context;

        public NewsService(DataContext context)
        {
            _context = context;
        }

        public async Task<Result<List<Models.News>>> GetAllNewsAsync()
        {
            try
            {
                var newsList = await _context.News
                    .OrderByDescending(n => n.DateCreated)
                    .ToListAsync();

                return Result<List<Models.News>>.Ok(newsList);
            }
            catch (Exception ex)
            {
                return Result<List<Models.News>>.BadRequest($"Failed to retrieve news: {ex.Message}");
            }
        }

        public async Task<Result<Models.News>> CreateNewsAsync(CreateNewsDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
            {
                return Result<Models.News>.BadRequest("Title is required");
            }

            if (string.IsNullOrWhiteSpace(dto.Text))
            {
                return Result<Models.News>.BadRequest("Text content is required");
            }

            try
            {
                var newsItem = new Models.News
                {
                    Title = dto.Title.Trim(),
                    Text = dto.Text.Trim(),
                    DateCreated = DateTime.UtcNow
                };

                _context.News.Add(newsItem);
                await _context.SaveChangesAsync();

                return Result<Models.News>.Ok(newsItem);
            }
            catch (Exception ex)
            {
                return Result<Models.News>.BadRequest($"Failed to create news item: {ex.Message}");
            }
        }

        public async Task<Result<string>> DeleteNewsAsync(int id)
        {
            try
            {
                var newsItem = await _context.News.FindAsync(id);
                if (newsItem == null)
                {
                    return Result<string>.NotFound("News item not found");
                }

                _context.News.Remove(newsItem);
                await _context.SaveChangesAsync();

                return Result<string>.Ok("News item deleted successfully");
            }
            catch (Exception ex)
            {
                return Result<string>.BadRequest($"Failed to delete news item: {ex.Message}");
            }
        }
    }
}
