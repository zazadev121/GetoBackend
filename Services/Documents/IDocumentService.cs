using apiprojnew.Common;
using apiprojnew.DTO;
using apiprojnew.Enum;
using Microsoft.AspNetCore.Http;

namespace apiprojnew.Services.Documents
{
    public interface IDocumentService
    {
        Task<Result<int>> UploadDocumentAsync(int userId, IFormFile file);
        Task<Result<byte[]>> GetDocumentAsync(int documentId, int userId);
        Task<Result<List<DocumentDTO>>> GetUserDocumentsAsync(int userId);
        Task<Result<DocumentDTO>> GetDocumentByIdAsync(int documentId, int userId);
        Task<Result<List<DocumentDTO>>> GetUserVisibleDocumentsAsync(int userId);
        Task<Result<List<DocumentDTO>>> GetAdminDocumentsByUserPhaseAsync(int userId);
        Task<Result<byte[]>> GetAdminDocumentAsync(int documentId, int userId);
        Task<Result<string>> DeleteDocumentAsync(int documentId, int userId);
        Task<Result<(byte[] data, string contentType, string fileName)>> GetDocumentDirectAsync(int documentId);
    }
}
