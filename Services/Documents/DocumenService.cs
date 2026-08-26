using apiprojnew.Common;
using apiprojnew.Data;
using apiprojnew.DTO;
using apiprojnew.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace apiprojnew.Services.Documents
{
    public class DocumentService : IDocumentService
    {
        private readonly DataContext _db;
        private readonly IConfiguration _configuration;
        private readonly string _uploadPath;

        // Allowed file extensions
        private readonly string[] AllowedExtensions = { ".pdf", ".docx", ".doc" };
        private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB

        public DocumentService(DataContext db, IConfiguration configuration)
        {
            _db = db;
            _configuration = configuration;
            _uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
            
            // Create Uploads directory if it doesn't exist
            if (!Directory.Exists(_uploadPath))
            {
                Directory.CreateDirectory(_uploadPath);
            }
        }

        public async Task<Result<int>> UploadDocumentAsync(int userId, IFormFile file)
        {
            // Validate user exists
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return Result<int>.BadRequest("User not found");
            }

            // Validate file
            if (file == null || file.Length == 0)
            {
                return Result<int>.BadRequest("No file provided");
            }

            // Check file size
            if (file.Length > MaxFileSize)
            {
                return Result<int>.BadRequest("File size exceeds 10 MB limit");
            }

            // Check file extension
            var fileExtension = Path.GetExtension(file.FileName).ToLower();
            if (!AllowedExtensions.Contains(fileExtension))
            {
                return Result<int>.BadRequest("Only PDF and DOCX files are allowed");
            }

            try
            {
                // Read file into byte array
                byte[] fileData;
                using (var memoryStream = new MemoryStream())
                {
                    await file.CopyToAsync(memoryStream);
                    fileData = memoryStream.ToArray();
                }

                // Create document record with default phase
                var document = new Document
                {
                    UserId = userId,
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    FileData = fileData,
                    UploadedAt = DateTime.UtcNow,
                    Phase = Enum.UserPahse.phaseone
                };

                _db.Documents.Add(document);
                await _db.SaveChangesAsync();

                return Result<int>.Ok(document.Id);
            }
            catch (Exception ex)
            {
                return Result<int>.BadRequest($"Error uploading file: {ex.Message}");
            }
        }

        public async Task<Result<byte[]>> GetDocumentAsync(int documentId, int userId)
        {
            var document = await _db.Documents.FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId);
            
            if (document == null)
            {
                return Result<byte[]>.NotFound("Document not found or you don't have access to it");
            }

            return Result<byte[]>.Ok(document.FileData);
        }

        public async Task<Result<List<DocumentDTO>>> GetUserDocumentsAsync(int userId)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return Result<List<DocumentDTO>>.BadRequest("User not found");
            }

            var documents = await _db.Documents
                .Where(d => d.UserId == userId)
                .OrderByDescending(d => d.UploadedAt)
                .Select(d => new DocumentDTO
                {
                    Id = d.Id,
                    FileName = d.FileName,
                    ContentType = d.ContentType,
                    FileSize = d.FileData.Length,
                    UploadedAt = d.UploadedAt,
                    Phase = d.Phase
                })
                .ToListAsync();

            return Result<List<DocumentDTO>>.Ok(documents);
        }

        public async Task<Result<string>> DeleteDocumentAsync(int documentId, int userId)
        {
            var document = await _db.Documents.FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId);
            
            if (document == null)
            {
                return Result<string>.NotFound("Document not found or you don't have access to it");
            }

            try
            {
                _db.Documents.Remove(document);
                await _db.SaveChangesAsync();
                return Result<string>.Ok("Document deleted successfully");
            }
            catch (Exception ex)
            {
                return Result<string>.BadRequest($"Error deleting document: {ex.Message}");
            }
        }

        public async Task<Result<DocumentDTO>> GetDocumentByIdAsync(int documentId, int userId)
        {
            var document = await _db.Documents.FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId);
            
            if (document == null)
            {
                return Result<DocumentDTO>.NotFound("Document not found or you don't have access to it");
            }

            var documentDto = new DocumentDTO
            {
                Id = document.Id,
                FileName = document.FileName,
                ContentType = document.ContentType,
                FileSize = document.FileData.Length,
                UploadedAt = document.UploadedAt,
                Phase = document.Phase
            };

            return Result<DocumentDTO>.Ok(documentDto);
        }

        public async Task<Result<List<DocumentDTO>>> GetUserVisibleDocumentsAsync(int userId)
        {
            // Get the user and their current phase
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return Result<List<DocumentDTO>>.BadRequest("User not found");
            }

            try
            {
                // Get all documents that match the user's current phase
                var documents = await _db.Documents
                    .Where(d => d.Phase == user.UserPahse)
                    .OrderByDescending(d => d.UploadedAt)
                    .Select(d => new DocumentDTO
                    {
                        Id = d.Id,
                        FileName = d.FileName,
                        ContentType = d.ContentType,
                        FileSize = d.FileData.Length,
                        UploadedAt = d.UploadedAt,
                        Phase = d.Phase
                    })
                    .ToListAsync();

                if (!documents.Any())
                {
                    return Result<List<DocumentDTO>>.NotFound($"No documents found for your current phase ({user.UserPahse})");
                }

                return Result<List<DocumentDTO>>.Ok(documents);
            }
            catch (Exception ex)
            {
                return Result<List<DocumentDTO>>.BadRequest($"Error retrieving documents: {ex.Message}");
            }
        }

        public async Task<Result<List<DocumentDTO>>> GetAdminDocumentsByUserPhaseAsync(int userId)
        {
            // Get the user and their current phase
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return Result<List<DocumentDTO>>.BadRequest("User not found");
            }

            try
            {
                // Get all documents that match the user's current phase
                var documents = await _db.Documents
                    .Where(d => d.Phase == user.UserPahse)
                    .OrderByDescending(d => d.UploadedAt)
                    .Select(d => new DocumentDTO
                    {
                        Id = d.Id,
                        FileName = d.FileName,
                        ContentType = d.ContentType,
                        FileSize = d.FileData.Length,
                        UploadedAt = d.UploadedAt,
                        Phase = d.Phase
                    })
                    .ToListAsync();

                if (!documents.Any())
                {
                    return Result<List<DocumentDTO>>.NotFound($"No documents found for your current phase ({user.UserPahse})");
                }

                return Result<List<DocumentDTO>>.Ok(documents);
            }
            catch (Exception ex)
            {
                return Result<List<DocumentDTO>>.BadRequest($"Error retrieving documents: {ex.Message}");
            }
        }

        public async Task<Result<byte[]>> GetAdminDocumentAsync(int documentId, int userId)
        {
            // Get the user and their current phase
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return Result<byte[]>.BadRequest("User not found");
            }

            // Get document only if it matches user's phase
            var document = await _db.Documents.FirstOrDefaultAsync(d => 
                d.Id == documentId && d.Phase == user.UserPahse);
            
            if (document == null)
            {
                return Result<byte[]>.NotFound("Document not found or not available for your current phase");
            }

            return Result<byte[]>.Ok(document.FileData);
        }
    }
}
