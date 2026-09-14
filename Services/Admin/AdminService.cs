using apiprojnew.Common;
using apiprojnew.Data;
using apiprojnew.DTO;
using apiprojnew.Enum;
using Microsoft.EntityFrameworkCore;
using WebPush;

namespace apiprojnew.Services.Admin
{
    public class AdminService : IAdminService
    {
        private static List<DocumentDTO> DeduplicateDocuments(IEnumerable<DocumentDTO> documents)
        {
            return documents
                .GroupBy(d => d.FileName.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(d => d.UploadedAt).First())
                .OrderByDescending(d => d.UploadedAt)
                .ToList();
        }

        private readonly DataContext _db;
        private readonly WebPushService _pushService;

        public AdminService(DataContext db, WebPushService pushService)
        {
            _db = db;
            _pushService = pushService;
        }

        public async Task<Result<int>> AddDocumentForAllUsersAsync(string fileName, string contentType, byte[] fileData, UserPahse phase)
        {
            if (fileData == null || fileData.Length == 0)
                return Result<int>.BadRequest("No file data provided");

            try
            {
                var allUsers = await _db.Users.ToListAsync();
                if (!allUsers.Any())
                    return Result<int>.BadRequest("No users found in the system");

                var normalizedFileName = fileName.Trim();

                var existingUserIds = (await _db.Documents
                    .Where(d => d.Phase == phase && d.FileName.ToLower() == normalizedFileName.ToLower())
                    .Select(d => d.UserId)
                    .Distinct()
                    .ToListAsync()).ToHashSet();

                var newDocuments = allUsers
                    .Where(user => !existingUserIds.Contains(user.Id))
                    .Select(user => new Models.Document
                    {
                        UserId = user.Id,
                        FileName = normalizedFileName,
                        ContentType = contentType,
                        FileData = fileData,
                        UploadedAt = DateTime.UtcNow,
                        Phase = phase,
                        IsAdminUploaded = true
                    }).ToList();

                if (!newDocuments.Any())
                    return Result<int>.Ok(0);

                _db.Documents.AddRange(newDocuments);
                await _db.SaveChangesAsync();

                await _pushService.SendToAllAsync(
                    "📎 ახალი ფაილი — GETO Project",
                    $"ახალი დოკუმენტი დაემატა თქვენს კაბინეტში: {fileName}",
                    "/dashboard"
                );

                return Result<int>.Ok(newDocuments.Count);
            }
            catch (Exception ex)
            {
                return Result<int>.BadRequest($"Error adding document for users: {ex.Message}");
            }
        }

        public async Task<Result<int>> SendDocumentToSingleUserAsync(int userId, string fileName, string contentType, byte[] fileData, UserPahse phase, string? adminNote = null)
        {
            if (fileData == null || fileData.Length == 0)
                return Result<int>.BadRequest("No file data provided");

            try
            {
                var user = await _db.Users.FindAsync(userId);
                if (user == null)
                    return Result<int>.NotFound("User not found");

                var normalizedFileName = fileName.Trim();
                var existingDocument = await _db.Documents
                    .FirstOrDefaultAsync(d => d.UserId == user.Id && d.Phase == phase && d.FileName.ToLower() == normalizedFileName.ToLower());

                if (existingDocument != null)
                    return Result<int>.Ok(existingDocument.Id);

                var document = new Models.Document
                {
                    UserId = user.Id,
                    FileName = normalizedFileName,
                    ContentType = contentType,
                    FileData = fileData,
                    UploadedAt = DateTime.UtcNow,
                    Phase = phase,
                    IsAdminUploaded = true
                };

                _db.Documents.Add(document);
                await _db.SaveChangesAsync();

                // Push notification only — no email
                await _pushService.SendToUserAsync(
                    user.Id,
                    "📎 ახალი ფაილი ადმინისტრაციისგან",
                    $"თქვენს პირად კაბინეტში დაემატა ახალი ფაილი: {fileName}",
                    "/dashboard"
                );

                return Result<int>.Ok(1);
            }
            catch (Exception ex)
            {
                return Result<int>.BadRequest($"Error sending document to user: {ex.Message}");
            }
        }

        public async Task<Result<List<UserWithDocumentsDTO>>> GetAllUsersWithDocumentsAsync()
        {
            try
            {
                var users = await _db.Users.Include(u => u.Documents).ToListAsync();
                return Result<List<UserWithDocumentsDTO>>.Ok(users.Select(u => MapUserToDTO(u)).ToList());
            }
            catch (Exception ex)
            {
                return Result<List<UserWithDocumentsDTO>>.BadRequest($"Error retrieving users: {ex.Message}");
            }
        }

        public async Task<Result<List<UserWithDocumentsDTO>>> SearchUsersByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Result<List<UserWithDocumentsDTO>>.BadRequest("Search name cannot be empty");

            try
            {
                var users = await _db.Users
                    .Include(u => u.Documents)
                    .Where(u => u.Name.Contains(name) || u.LastName.Contains(name) || u.Email.Contains(name))
                    .ToListAsync();

                if (!users.Any())
                    return Result<List<UserWithDocumentsDTO>>.NotFound("No users found matching the search criteria");

                return Result<List<UserWithDocumentsDTO>>.Ok(users.Select(u => MapUserToDTO(u)).ToList());
            }
            catch (Exception ex)
            {
                return Result<List<UserWithDocumentsDTO>>.BadRequest($"Error searching users: {ex.Message}");
            }
        }

        public async Task<Result<UserWithDocumentsDTO>> GetUserWithDocumentsByIdAsync(int userId)
        {
            try
            {
                var user = await _db.Users.Include(u => u.Documents).FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                    return Result<UserWithDocumentsDTO>.NotFound("User not found");

                return Result<UserWithDocumentsDTO>.Ok(MapUserToDTO(user));
            }
            catch (Exception ex)
            {
                return Result<UserWithDocumentsDTO>.BadRequest($"Error retrieving user: {ex.Message}");
            }
        }

        public async Task<Result<byte[]>> DownloadUserDocumentAsync(int documentId, int userId)
        {
            try
            {
                var document = await _db.Documents.FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId);
                if (document == null)
                    return Result<byte[]>.NotFound("Document not found for this user");

                return Result<byte[]>.Ok(document.FileData);
            }
            catch (Exception ex)
            {
                return Result<byte[]>.BadRequest($"Error downloading document: {ex.Message}");
            }
        }

        public async Task<Result<string>> UpdateUserStatusAsync(int userId, userstatus status, string? comment = null)
        {
            try
            {
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                    return Result<string>.NotFound("User not found");

                var oldStatus = user.Status;
                user.Status = status;
                _db.Users.Update(user);
                await _db.SaveChangesAsync();

                if (oldStatus != status)
                {
                    string pushBody = GetStatusPushBody(status);
                    if (!string.IsNullOrWhiteSpace(comment))
                        pushBody += $" | {comment.Trim()}";
                    await _pushService.SendToUserAsync(userId, GetStatusPushTitle(status), pushBody, "/dashboard");
                }

                return Result<string>.Ok($"User status updated to {status}");
            }
            catch (Exception ex)
            {
                return Result<string>.BadRequest($"Error updating user status: {ex.Message}");
            }
        }

        public async Task<Result<string>> UpdateUserPhaseAsync(int userId, UserPahse phase, string? comment = null)
        {
            try
            {
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                    return Result<string>.NotFound("User not found");

                var oldPhase = user.UserPahse;
                user.UserPahse = phase;
                _db.Users.Update(user);
                await _db.SaveChangesAsync();

                if (oldPhase != phase)
                {
                    string pushBody = GetPhasePushBody(phase);
                    if (!string.IsNullOrWhiteSpace(comment))
                        pushBody += $" | {comment.Trim()}";
                    await _pushService.SendToUserAsync(userId, GetPhasePushTitle(phase), pushBody, "/dashboard");
                }

                return Result<string>.Ok($"User phase updated to {phase}");
            }
            catch (Exception ex)
            {
                return Result<string>.BadRequest($"Error updating user phase: {ex.Message}");
            }
        }

        public async Task<Result<string>> UpdateUserMaxFileSizeAsync(int userId, int maxFileSizeMb)
        {
            try
            {
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                    return Result<string>.NotFound("User not found");

                if (maxFileSizeMb <= 0)
                {
                    maxFileSizeMb = 25; // Default fallback
                }

                user.MaxFileSizeMb = maxFileSizeMb;
                _db.Users.Update(user);
                await _db.SaveChangesAsync();

                return Result<string>.Ok($"User max file size limit updated to {maxFileSizeMb} MB");
            }
            catch (Exception ex)
            {
                return Result<string>.BadRequest($"Error updating user max file size: {ex.Message}");
            }
        }

        public async Task<Result<string>> DeleteUserWithDocumentsAsync(int userId)
        {
            try
            {
                var user = await _db.Users.Include(u => u.Documents).FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                    return Result<string>.NotFound("User not found");

                _db.Documents.RemoveRange(user.Documents);
                _db.Users.Remove(user);
                await _db.SaveChangesAsync();

                return Result<string>.Ok("User and all associated documents deleted successfully");
            }
            catch (Exception ex)
            {
                return Result<string>.BadRequest($"Error deleting user: {ex.Message}");
            }
        }

        public async Task<Result<string>> DeleteUserDocumentsOnlyAsync(int userId)
        {
            try
            {
                var user = await _db.Users.Include(u => u.Documents).FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                    return Result<string>.NotFound("User not found");

                if (!user.Documents.Any())
                    return Result<string>.NotFound("User has no documents to delete");

                _db.Documents.RemoveRange(user.Documents);
                await _db.SaveChangesAsync();

                return Result<string>.Ok($"All {user.Documents.Count} document(s) deleted successfully. User account remains active.");
            }
            catch (Exception ex)
            {
                return Result<string>.BadRequest($"Error deleting user documents: {ex.Message}");
            }
        }

        public async Task<Result<string>> DeleteDocumentByIdAsync(int documentId)
        {
            try
            {
                var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == documentId);
                if (doc == null)
                    return Result<string>.NotFound("Document not found");

                _db.Documents.Remove(doc);
                await _db.SaveChangesAsync();

                return Result<string>.Ok($"Document {doc.FileName} (ID: {documentId}) deleted successfully.");
            }
            catch (Exception ex)
            {
                return Result<string>.BadRequest($"Error deleting document: {ex.Message}");
            }
        }

        public async Task<Result<string>> DeleteBulkDocumentsByFileNameAsync(string fileName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fileName))
                    return Result<string>.BadRequest("File name cannot be empty");

                var docs = await _db.Documents.Where(d => d.FileName.ToLower() == fileName.ToLower()).ToListAsync();
                if (!docs.Any())
                    return Result<string>.NotFound($"No template documents found with file name '{fileName}'");

                int count = docs.Count;
                _db.Documents.RemoveRange(docs);
                await _db.SaveChangesAsync();

                return Result<string>.Ok($"Successfully deleted {count} copy(ies) of template document '{fileName}' from the database.");
            }
            catch (Exception ex)
            {
                return Result<string>.BadRequest($"Error deleting template documents: {ex.Message}");
            }
        }

        public async Task<Result<bool>> ToggleDocumentAdminUploadedAsync(int documentId)
        {
            var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == documentId);
            if (doc == null)
                return Result<bool>.NotFound("Document not found");

            doc.IsAdminUploaded = !doc.IsAdminUploaded;
            await _db.SaveChangesAsync();
            return Result<bool>.Ok(doc.IsAdminUploaded);
        }

        // ─── Helpers ────────────────────────────────────────────────────────────

        private static UserWithDocumentsDTO MapUserToDTO(Models.User user)
        {
            var documents = DeduplicateDocuments(user.Documents.Select(d => new DocumentDTO
            {
                Id = d.Id,
                FileName = d.FileName,
                ContentType = d.ContentType,
                FileSize = d.FileData.Length,
                UploadedAt = d.UploadedAt,
                Phase = d.Phase,
                IsAdminUploaded = d.IsAdminUploaded
            })).ToList();

            return new UserWithDocumentsDTO
            {
                Id = user.Id,
                Name = user.Name,
                LastName = user.LastName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                Status = user.Status,
                UserPhase = user.UserPahse,
                IsVerified = user.IsVerified,
                MaxFileSizeMb = user.MaxFileSizeMb <= 0 ? 25 : user.MaxFileSizeMb,
                Documents = documents
            };
        }

        // ─── Push notification label helpers ────────────────────────────────────
        private static string GetStatusPushTitle(userstatus status) => status switch
        {
            userstatus.Approved     => "✅ სტატუსი განახლდა — GETO Project",
            userstatus.Rejected     => "❌ სტატუსი განახლდა — GETO Project",
            userstatus.Resubmission => "🔄 ხელახლა წარდგენა — GETO Project",
            _                       => "🔔 სტატუსი განახლდა — GETO Project"
        };

        private static string GetStatusPushBody(userstatus status) => status switch
        {
            userstatus.Approved     => "გილოცავთ! თქვენი დოკუმენტაცია დადასტურებულია.",
            userstatus.Rejected     => "თქვენი განაცხადი უარყოფილია. დეტალებისთვის გადადით კაბინეტში.",
            userstatus.Resubmission => "საჭიროა დოკუმენტების ხელახლა გამოგზავნა. გადახედეთ კაბინეტს.",
            _                       => "თქვენი სტატუსი შეიცვალა. გადახედეთ პირად კაბინეტს."
        };

        private static string GetPhasePushTitle(UserPahse phase) => phase switch
        {
            UserPahse.phaseone      => "📋 I ეტაპი — GETO Project",
            UserPahse.phasetwo      => "📋 II ეტაპი — GETO Project",
            UserPahse.phasethree    => "📋 III ეტაპი — GETO Project",
            UserPahse.phaseCanceled => "📋 ეტაპი გაუქმდა — GETO Project",
            _                       => "📋 ეტაპი განახლდა — GETO Project"
        };

        private static string GetPhasePushBody(UserPahse phase) => phase switch
        {
            UserPahse.phaseone      => "გთხოვთ, ატვირთოთ თქვენი რეზიუმე (CV).\n\nCV-ის ატვირთვის შემდეგ გამოგეგზავნებათ ხელშეკრულება ხელმოსაწერად",
            UserPahse.phasetwo      => "გთხოვთ, გაეცნოთ იანვარში წარმოსადგენი დოკუმენტების ნუსხას და მოამზადოთ ყველა საჭირო დოკუმენტი",
            UserPahse.phasethree    => "გთხოვთ, ხელი მოაწეროთ ხელშეკრულებას.\n\nხელმოწერის შემდეგ სამუშაო ნებართვა აგეტვირთებათ თქვენს პროფილში.",
            UserPahse.phaseCanceled => "თქვენი ეტაპი გაუქმებულია. კითხვებისთვის დაგვიკავშირდით.",
            _                       => "თქვენი ეტაპი განახლდა."
        };
    }
}
