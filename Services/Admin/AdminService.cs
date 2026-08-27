using apiprojnew.Common;
using apiprojnew.Data;
using apiprojnew.DTO;
using apiprojnew.Enum;
using Microsoft.EntityFrameworkCore;

namespace apiprojnew.Services.Admin
{
    public class AdminService : IAdminService
    {
        private readonly DataContext _db;
        private readonly SmtpService _smtpService;

        public AdminService(DataContext db, SmtpService smtpService)
        {
            _db = db;
            _smtpService = smtpService;
        }

        public async Task<Result<int>> AddDocumentForAllUsersAsync(string fileName, string contentType, byte[] fileData, UserPahse phase)
        {
            // Validate file data
            if (fileData == null || fileData.Length == 0)
            {
                return Result<int>.BadRequest("No file data provided");
            }

            try
            {
                // Get all users
                var allUsers = await _db.Users.ToListAsync();
                if (!allUsers.Any())
                {
                    return Result<int>.BadRequest("No users found in the system");
                }

                // Create a document for each user with the specified phase
                var documents = allUsers.Select(user => new Models.Document
                {
                    UserId = user.Id,
                    FileName = fileName,
                    ContentType = contentType,
                    FileData = fileData,
                    UploadedAt = DateTime.UtcNow,
                    Phase = phase
                }).ToList();

                _db.Documents.AddRange(documents);
                await _db.SaveChangesAsync();

                return Result<int>.Ok(documents.Count);
            }
            catch (Exception ex)
            {
                return Result<int>.BadRequest($"Error adding document for users: {ex.Message}");
            }
        }

        public async Task<Result<List<UserWithDocumentsDTO>>> GetAllUsersWithDocumentsAsync()
        {
            try
            {
                var users = await _db.Users
                    .Include(u => u.Documents)
                    .ToListAsync();

                var userDtos = users.Select(u => MapUserToDTO(u)).ToList();

                return Result<List<UserWithDocumentsDTO>>.Ok(userDtos);
            }
            catch (Exception ex)
            {
                return Result<List<UserWithDocumentsDTO>>.BadRequest($"Error retrieving users: {ex.Message}");
            }
        }

        public async Task<Result<List<UserWithDocumentsDTO>>> SearchUsersByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Result<List<UserWithDocumentsDTO>>.BadRequest("Search name cannot be empty");
            }

            try
            {
                var users = await _db.Users
                    .Include(u => u.Documents)
                    .Where(u => u.Name.Contains(name) || u.LastName.Contains(name) || u.Email.Contains(name))
                    .ToListAsync();

                if (!users.Any())
                {
                    return Result<List<UserWithDocumentsDTO>>.NotFound("No users found matching the search criteria");
                }

                var userDtos = users.Select(u => MapUserToDTO(u)).ToList();
                return Result<List<UserWithDocumentsDTO>>.Ok(userDtos);
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
                var user = await _db.Users
                    .Include(u => u.Documents)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                {
                    return Result<UserWithDocumentsDTO>.NotFound("User not found");
                }

                var userDto = MapUserToDTO(user);
                return Result<UserWithDocumentsDTO>.Ok(userDto);
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
                var document = await _db.Documents.FirstOrDefaultAsync(d => 
                    d.Id == documentId && d.UserId == userId);

                if (document == null)
                {
                    return Result<byte[]>.NotFound("Document not found for this user");
                }

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
                {
                    return Result<string>.NotFound("User not found");
                }

                var oldStatus = user.Status;
                user.Status = status;
                _db.Users.Update(user);
                await _db.SaveChangesAsync();

                if (oldStatus != status)
                {
                    SendStatusNotificationEmail(user, oldStatus, status, comment);
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
                {
                    return Result<string>.NotFound("User not found");
                }

                var oldPhase = user.UserPahse;
                user.UserPahse = phase;
                _db.Users.Update(user);
                await _db.SaveChangesAsync();

                if (oldPhase != phase)
                {
                    SendPhaseNotificationEmail(user, oldPhase, phase, comment);
                }

                return Result<string>.Ok($"User phase updated to {phase}");
            }
            catch (Exception ex)
            {
                return Result<string>.BadRequest($"Error updating user phase: {ex.Message}");
            }
        }

        public async Task<Result<string>> DeleteUserWithDocumentsAsync(int userId)
        {
            try
            {
                var user = await _db.Users
                    .Include(u => u.Documents)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                {
                    return Result<string>.NotFound("User not found");
                }

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
                var user = await _db.Users
                    .Include(u => u.Documents)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                {
                    return Result<string>.NotFound("User not found");
                }

                if (!user.Documents.Any())
                {
                    return Result<string>.NotFound("User has no documents to delete");
                }

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
                {
                    return Result<string>.NotFound("Document not found");
                }

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
                {
                    return Result<string>.BadRequest("File name cannot be empty");
                }

                var docs = await _db.Documents.Where(d => d.FileName.ToLower() == fileName.ToLower()).ToListAsync();
                if (!docs.Any())
                {
                    return Result<string>.NotFound($"No template documents found with file name '{fileName}'");
                }

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

        private void SendStatusNotificationEmail(Models.User user, userstatus oldStatus, userstatus newStatus, string? comment)
        {
            SendAccountUpdateEmail(user, changedField: "status", oldStatus: oldStatus, newStatus: newStatus, oldPhase: null, newPhase: user.UserPahse, comment: comment);
        }

        private void SendPhaseNotificationEmail(Models.User user, UserPahse oldPhase, UserPahse newPhase, string? comment)
        {
            SendAccountUpdateEmail(user, changedField: "phase", oldStatus: null, newStatus: user.Status, oldPhase: oldPhase, newPhase: newPhase, comment: comment);
        }

        private void SendAccountUpdateEmail(Models.User user, string changedField, userstatus? oldStatus, userstatus newStatus, UserPahse? oldPhase, UserPahse newPhase, string? comment)
        {
            try
            {
                bool isStatusChanged = changedField == "status" && oldStatus.HasValue;
                bool isPhaseChanged = changedField == "phase" && oldPhase.HasValue;

                string currentStatusTitle = GetStatusTitle(newStatus);
                string currentPhaseTitle = GetPhaseTitle(newPhase);

                string changeSummaryKa = isStatusChanged 
                    ? $"შეიცვალა თქვენი ანგარიშის სტატუსი ({GetStatusTitle(oldStatus!.Value)} ➔ {currentStatusTitle})" 
                    : $"შეიცვალა თქვენი მიმდინარე ეტაპი ({GetPhaseTitle(oldPhase!.Value)} ➔ {currentPhaseTitle})";

                string detailMessageKa = isStatusChanged 
                    ? GetStatusDetailMessageKa(newStatus) 
                    : GetPhaseDetailMessageKa(newPhase);

                string subject = isStatusChanged
                    ? $"GETO Project: სტატუსი შეიცვალა — {currentStatusTitle}"
                    : $"GETO Project: ეტაპი შეიცვალა — {currentPhaseTitle}";

                string statusLineText = isStatusChanged
                    ? $"  • სტატუსი (STATUS): {currentStatusTitle}  <--- [შეიცვალა / CHANGED] (ძველი: {GetStatusTitle(oldStatus!.Value)})"
                    : $"  • სტატუსი (STATUS): {currentStatusTitle}  (უცვლელი / Unchanged)";

                string phaseLineText = isPhaseChanged
                    ? $"  • ეტაპი (PHASE):   {currentPhaseTitle}  <--- [შეიცვალა / CHANGED] (ძველი: {GetPhaseTitle(oldPhase!.Value)})"
                    : $"  • ეტაპი (PHASE):   {currentPhaseTitle}  (უცვლელი / Unchanged)";

                string plainText = $"გამარჯობა {user.Name} {user.LastName},\n\n" +
                                   $"თქვენს ანგარიშზე განხორციელდა ცვლილება: {changeSummaryKa}\n\n" +
                                   $"ანგარიშის მიმდინარე მონაცემები:\n" +
                                   $"{statusLineText}\n" +
                                   $"{phaseLineText}\n\n" +
                                   $"დეტალური ინფორმაცია:\n{detailMessageKa}\n\n" +
                                   $"დეტალებისთვის გთხოვთ ეწვიოთ თქვენს პირად კაბინეტს (GETO Project).";

                string statusRowHtml = isStatusChanged ? $@"
                    <tr style='background-color: #0284c720; border-left: 4px solid #38bdf8;'>
                        <td style='padding: 12px; font-weight: bold; color: #38bdf8;'>სტატუსი / Status</td>
                        <td style='padding: 12px; color: #ffffff;'>
                            <span style='background-color: #0284c7; color: #fff; padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: bold; margin-right: 6px;'>CHANGED / შეიცვალა</span>
                            <strong>{currentStatusTitle}</strong>
                            <br/><span style='font-size: 12px; color: #94a3b8;'>ძველი / Previous: {GetStatusTitle(oldStatus!.Value)}</span>
                        </td>
                    </tr>" : $@"
                    <tr style='border-bottom: 1px solid #334155;'>
                        <td style='padding: 12px; font-weight: bold; color: #94a3b8;'>სტატუსი / Status</td>
                        <td style='padding: 12px; color: #cbd5e1;'>
                            {currentStatusTitle} <span style='font-size: 11px; color: #64748b;'>(უცვლელი / Unchanged)</span>
                        </td>
                    </tr>";

                string phaseRowHtml = isPhaseChanged ? $@"
                    <tr style='background-color: #ec489920; border-left: 4px solid #ec4899;'>
                        <td style='padding: 12px; font-weight: bold; color: #f472b6;'>ეტაპი / Phase</td>
                        <td style='padding: 12px; color: #ffffff;'>
                            <span style='background-color: #db2777; color: #fff; padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: bold; margin-right: 6px;'>CHANGED / შეიცვალა</span>
                            <strong>{currentPhaseTitle}</strong>
                            <br/><span style='font-size: 12px; color: #94a3b8;'>ძველი / Previous: {GetPhaseTitle(oldPhase!.Value)}</span>
                        </td>
                    </tr>" : $@"
                    <tr style='border-bottom: 1px solid #334155;'>
                        <td style='padding: 12px; font-weight: bold; color: #94a3b8;'>ეტაპი / Phase</td>
                        <td style='padding: 12px; color: #cbd5e1;'>
                            {currentPhaseTitle} <span style='font-size: 11px; color: #64748b;'>(უცვლელი / Unchanged)</span>
                        </td>
                    </tr>";

                string headerBadgeColor = isStatusChanged ? "#38bdf8" : "#ec4899";

                string htmlContent = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #1e293b; border-radius: 12px; background-color: #0f172a; color: #f8fafc;'>
                    <div style='text-align: center; padding-bottom: 15px; border-bottom: 1px solid #334155;'>
                        <h2 style='color: {headerBadgeColor}; margin: 0;'>GETO Project LLC</h2>
                        <p style='color: #94a3b8; font-size: 14px; margin-top: 5px;'>ანგარიშის განახლების შეტყობინება / Account Update Notice</p>
                    </div>
                    <div style='padding: 20px 0;'>
                        <p style='font-size: 16px; color: #f1f5f9;'>მოგესალმებით, <strong>{user.Name} {user.LastName}</strong>!</p>
                        <p style='font-size: 14px; color: #cbd5e1; line-height: 1.6;'>
                            თქვენს ანგარიშზე განხორციელდა ცვლილება: <strong>{changeSummaryKa}</strong>
                        </p>

                        <!-- Status & Phase Overview Table -->
                        <table style='width: 100%; border-collapse: collapse; margin: 20px 0; background-color: #1e293b; border-radius: 8px; overflow: hidden; font-size: 14px;'>
                            {statusRowHtml}
                            {phaseRowHtml}
                        </table>

                        <div style='background-color: #0284c715; padding: 14px; border-radius: 8px; border: 1px solid #0284c730; margin-top: 15px;'>
                            <p style='margin: 0; font-size: 13px; color: #38bdf8; font-weight: bold;'>ინსტრუქცია / Guidance:</p>
                            <p style='margin: 6px 0 0 0; font-size: 13px; color: #e2e8f0; line-height: 1.5;'>{detailMessageKa}</p>
                        </div>
                    </div>
                    <div style='text-align: center; padding-top: 15px; border-top: 1px solid #334155; font-size: 12px; color: #64748b;'>
                        <p style='margin: 0;'>შპს გეთო ფროჯექთი (GETO Project LLC)</p>
                        <p style='margin: 5px 0 0 0;'>საკონტაქტო: +995 577 54 75 77 | getogeto2020@gmail.com</p>
                    </div>
                </div>";

                _smtpService.SendNotificationEmailAsync(subject, htmlContent, plainText, user.Email);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send update email: {ex.Message}");
            }
        }

        private static string GetStatusTitle(userstatus status)
        {
            return status switch
            {
                userstatus.Pending => "განხილვის პროცესში (Pending)",
                userstatus.Approved => "დადასტურებული (Approved)",
                userstatus.Rejected => "უარყოფილი (Rejected)",
                userstatus.Resubmission => "ხელახლა წარდგენა (Resubmission)",
                _ => status.ToString()
            };
        }

        private static string GetStatusDetailMessageKa(userstatus status)
        {
            return status switch
            {
                userstatus.Approved => "გილოცავთ! თქვენი განაცხადი და დოკუმენტაცია წარმატებით დადასტურდა.",
                userstatus.Pending => "თქვენი დოკუმენტაცია მიღებულია და იმყოფება განხილვის პროცესში.",
                userstatus.Rejected => "თქვენი განაცხადი/დოკუმენტაცია არ დადასტურდა. დამატებითი ინფორმაციისთვის დაგვიკავშირდით.",
                userstatus.Resubmission => "თქვენს დოკუმენტაციას სჭირდება შესწორება. გთხოვთ, შეხვიდეთ პირად კაბინეტში და ხელახლა ატვირთოთ მოთხოვნილი ფაილები.",
                _ => "თქვენი სტატუსი განახლდა პირად კაბინეტში."
            };
        }

        private static string GetPhaseTitle(UserPahse phase)
        {
            return phase switch
            {
                UserPahse.phaseone => "I ეტაპი — რეგისტრაცია",
                UserPahse.phasetwo => "II ეტაპი — ხელშეკრულება და დოკუმენტაცია",
                UserPahse.phasethree => "III ეტაპი – სამუშაო ნებართვა და გამგზავრება",
                UserPahse.phaseCanceled => "გაუქმებული (Canceled)",
                _ => phase.ToString()
            };
        }

        private static string GetPhaseDetailMessageKa(UserPahse phase)
        {
            return phase switch
            {
                UserPahse.phaseone => "I ეტაპი: გთხოვთ ატვირთოთ თქვენი რეზიუმე (CV) პირად კაბინეტში.",
                UserPahse.phasetwo => "II ეტაპი: გთხოვთ პირად კაბინეტში ჩამოტვირთოთ ხელშეკრულება, სრულად შეავსოთ, მოაწეროთ ხელი და ატვირთოთ PDF ფორმატში.",
                UserPahse.phasethree => "III ეტაპი: გადმოგეცემათ გერმანიიდან მიღებული სამუშაო ნებართვა და დაიგეგმება გამგზავრების ორგანიზება.",
                UserPahse.phaseCanceled => "თქვენი ეტაპი გაუქმებულია. კითხვების შემთხვევაში დაგვიკავშირდით.",
                _ => "თქვენი ეტაპი განახლდა."
            };
        }

        private static UserWithDocumentsDTO MapUserToDTO(Models.User user)
        {
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
                Documents = user.Documents.Select(d => new DocumentDTO
                {
                    Id = d.Id,
                    FileName = d.FileName,
                    ContentType = d.ContentType,
                    FileSize = d.FileData.Length,
                    UploadedAt = d.UploadedAt,
                    Phase = d.Phase
                }).ToList()
            };
        }
    }
}
