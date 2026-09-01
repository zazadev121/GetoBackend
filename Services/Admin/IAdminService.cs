using apiprojnew.Common;
using apiprojnew.DTO;
using apiprojnew.Enum;

namespace apiprojnew.Services.Admin
{
    public interface IAdminService
    {
        // Document Management
        Task<Result<int>> AddDocumentForAllUsersAsync(string fileName, string contentType, byte[] fileData, UserPahse phase);
        Task<Result<int>> SendDocumentToSingleUserAsync(int userId, string fileName, string contentType, byte[] fileData, UserPahse phase, string? adminNote = null);
        
        // User Retrieval   
        Task<Result<List<UserWithDocumentsDTO>>> GetAllUsersWithDocumentsAsync();
        Task<Result<List<UserWithDocumentsDTO>>> SearchUsersByNameAsync(string name);
        Task<Result<UserWithDocumentsDTO>> GetUserWithDocumentsByIdAsync(int userId);
        
        // User Status and Phase Management
        Task<Result<string>> UpdateUserStatusAsync(int userId, userstatus status, string? comment = null);
        Task<Result<string>> UpdateUserPhaseAsync(int userId, UserPahse phase, string? comment = null);
        
        // Admin Download User Documents
        Task<Result<byte[]>> DownloadUserDocumentAsync(int documentId, int userId);
        
        // User and Document Deletion
        Task<Result<string>> DeleteUserWithDocumentsAsync(int userId);
        Task<Result<string>> DeleteUserDocumentsOnlyAsync(int userId);
        Task<Result<string>> DeleteDocumentByIdAsync(int documentId);
        Task<Result<string>> DeleteBulkDocumentsByFileNameAsync(string fileName);
    }
}
