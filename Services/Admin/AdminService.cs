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
        private readonly DataContext _db;
        private readonly SmtpService _smtpService;
        private readonly WebPushService _pushService;

        public AdminService(DataContext db, SmtpService smtpService, WebPushService pushService)
        {
            _db = db;
            _smtpService = smtpService;
            _pushService = pushService;
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
                    Phase = phase,
                    IsAdminUploaded = true
                }).ToList();

                _db.Documents.AddRange(documents);
                await _db.SaveChangesAsync();

                // Notify all users: new document added for their phase
                await _pushService.SendToAllAsync(
                    "📎 ახალი ფაილი — GETO Project",
                    $"ახალი დოკუმენტი დაემატა თქვენს კაბინეტში: {fileName}",
                    "/dashboard"
                );

                return Result<int>.Ok(documents.Count);
            }
            catch (Exception ex)
            {
                return Result<int>.BadRequest($"Error adding document for users: {ex.Message}");
            }
        }

        public async Task<Result<int>> SendDocumentToSingleUserAsync(int userId, string fileName, string contentType, byte[] fileData, UserPahse phase, string? adminNote = null)
        {
            if (fileData == null || fileData.Length == 0)
            {
                return Result<int>.BadRequest("No file data provided");
            }

            try
            {
                var user = await _db.Users.FindAsync(userId);
                if (user == null)
                {
                    return Result<int>.NotFound("User not found");
                }

                var document = new Models.Document
                {
                    UserId = user.Id,
                    FileName = fileName,
                    ContentType = contentType,
                    FileData = fileData,
                    UploadedAt = DateTime.UtcNow,
                    Phase = phase,
                    IsAdminUploaded = true
                };

                _db.Documents.Add(document);
                await _db.SaveChangesAsync();

                // Direct download link for email
                string directDownloadUrl = $"https://getobackend.onrender.com/getoProject/Document/direct-download/{document.Id}";

                // Send Push Notification
                await _pushService.SendToUserAsync(
                    user.Id,
                    "📎 ახალი ფაილი ადმინისტრაციისგან",
                    $"თქვენს პირად კაბინეტში დაემატა ახალი ფაილი: {fileName}",
                    "/dashboard"
                );

                // Send Email Notification
                string subject = "GETO Project: ახალი დოკუმენტი თქვენს კაბინეტში";
                string htmlContent = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 10px;'>
                        <h2 style='color: #1e3a8a;'>ახალი დოკუმენტი ადმინისტრაციისგან</h2>
                        <p>გამარჯობა {user.Name},</p>
                        <p>ადმინისტრაციამ თქვენს პირად კაბინეტში დაამატა ახალი ფაილი: <strong>{fileName}</strong>.</p>
                        {(string.IsNullOrEmpty(adminNote) ? "" : $"<div style='background-color: #f8fafc; padding: 15px; border-left: 4px solid #3b82f6; margin: 20px 0;'><strong>ადმინისტრატორის კომენტარი:</strong><br/>{adminNote}</div>")}
                        <p>ფაილის ჩამოსატვირთად შეგიძლიათ დააჭიროთ ქვემოთ მოცემულ ღილაკს ან გაიაროთ ავტორიზაცია კაბინეტში:</p>
                        <div style='margin-top: 25px;'>
                            <a href='{directDownloadUrl}' style='background-color: #059669; color: white; padding: 12px 20px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block; margin-right: 10px;'>📥 ფაილის ჩამოტვირთვა (Download)</a>
                            <a href='https://getoproject.com/login' style='background-color: #2563eb; color: white; padding: 12px 20px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;'>კაბინეტში შესვლა</a>
                        </div>
                    </div>
                ";
                string plainText = $"გამარჯობა {user.Name}, ადმინისტრაციამ თქვენს კაბინეტში დაამატა ახალი ფაილი: {fileName}. ჩამოსატვირთად: {directDownloadUrl}";
                
                _smtpService.SendNotificationEmailAsync(subject, htmlContent, plainText, user.Email, adminNote);

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
                    await _pushService.SendToUserAsync(userId, GetStatusPushTitle(status), GetStatusPushBody(status), "/dashboard");
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
                    await _pushService.SendToUserAsync(userId, GetPhasePushTitle(phase), GetPhasePushBody(phase), "/dashboard");
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
                string subject = "GETO Project: სტატუსის ცვლილება / Account Status Notice";

                string commentTextSection = "";
                string commentHtmlSection = "";

                if (!string.IsNullOrWhiteSpace(comment))
                {
                    var trimmedComment = comment.Trim();
                    commentTextSection = $"\n\n💬 ადმინისტრატორის კომენტარი:\n\"{trimmedComment}\"";
                    
                    commentHtmlSection = $@"
                    <div style='background-color: rgba(59, 130, 246, 0.1); border: 1px solid rgba(59, 130, 246, 0.3); border-radius: 12px; padding: 16px; margin: 20px 0;'>
                        <p style='margin: 0 0 6px 0; font-size: 13px; font-weight: bold; color: #60a5fa; text-transform: uppercase; letter-spacing: 0.05em;'>💬 ადმინისტრატორის კომენტარი / Admin Comment:</p>
                        <p style='margin: 0; font-size: 15px; color: #f8fafc; font-style: italic; line-height: 1.6;'>""{trimmedComment}""</p>
                    </div>";
                }

                string plainText = $"გამარჯობა {user.Name} {user.LastName},\n\n" +
                                   $"გთხოვთ ნახოთ მიმდინარე სტატუსი რომელიც შეიცვალა." +
                                   $"{commentTextSection}\n\n" +
                                   $"დეტალებისთვის გთხოვთ ეწვიოთ თქვენს პირად კაბინეტს (GETO Project).";

                string htmlContent = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='utf-8'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                </head>
                <body style='margin: 0; padding: 0; background-color: #090d16; font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif;'>
                    <div style='max-width: 560px; margin: 30px auto; background: #0f172a; border: 1px solid #1e293b; border-radius: 20px; overflow: hidden; box-shadow: 0 20px 40px rgba(0, 0, 0, 0.5); text-align: left;'>
                        
                        <!-- Header Gradient Banner -->
                        <div style='background: linear-gradient(135deg, #0052ff 0%, #3b82f6 50%, #6366f1 100%); padding: 32px 24px; text-align: center;'>
                            <div style='display: inline-block; width: 48px; height: 48px; background: rgba(255, 255, 255, 0.2); border-radius: 12px; line-height: 48px; color: #ffffff; font-size: 24px; margin-bottom: 12px;'>
                                🔔
                            </div>
                            <h1 style='color: #ffffff; font-size: 22px; font-weight: 800; margin: 0; letter-spacing: -0.02em;'>GETO Project LLC</h1>
                            <p style='color: rgba(255, 255, 255, 0.85); font-size: 13px; font-weight: 500; margin: 6px 0 0 0;'>სტატუსის ცვლილება / Account Status Notice</p>
                        </div>

                        <!-- Content Area -->
                        <div style='padding: 32px 28px;'>
                            <p style='font-size: 16px; color: #f1f5f9; margin: 0 0 16px 0; font-weight: 600;'>
                                მოგესალმებით, <span style='color: #60a5fa;'>{user.Name} {user.LastName}</span>!
                            </p>

                            <!-- Main Highlight Box -->
                            <div style='background: linear-gradient(135deg, rgba(30, 41, 59, 0.8) 0%, rgba(15, 23, 42, 0.9) 100%); border: 1px solid rgba(59, 130, 246, 0.3); border-radius: 14px; padding: 20px; text-align: center; margin-bottom: 20px;'>
                                <p style='font-size: 16px; color: #38bdf8; font-weight: 700; margin: 0; line-height: 1.6;'>
                                    გთხოვთ ნახოთ მიმდინარე სტატუსი რომელიც შეიცვალა
                                </p>
                            </div>

                            {commentHtmlSection}

                            <p style='font-size: 14px; color: #94a3b8; line-height: 1.6; margin: 24px 0 0 0; text-align: center;'>
                                დეტალური ინფორმაციის სანახავად გთხოვთ ეწვიოთ თქვენს პირად კაბინეტს.
                            </p>
                        </div>

                        <!-- Footer -->
                        <div style='background-color: #090d16; padding: 20px; text-align: center; border-top: 1px solid #1e293b;'>
                            <p style='font-size: 12px; color: #64748b; margin: 0; font-weight: 500;'>
                                © {DateTime.UtcNow.Year} შპს გეთო ფროჯექთი (GETO Project LLC)
                            </p>
                            <p style='font-size: 11px; color: #475569; margin: 6px 0 0 0;'>
                                საკონტაქტო: +995 577 54 75 77 | getogeto2020@gmail.com
                            </p>
                        </div>

                    </div>
                </body>
                </html>";

                _smtpService.SendNotificationEmailAsync(subject, htmlContent, plainText, user.Email, comment);
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
                userstatus.Rejected => "უარყოფილი (Rejected)",
                userstatus.Approved => "დადასტურებული (Approved)",
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
                    Phase = d.Phase,
                    IsAdminUploaded = d.IsAdminUploaded
                }).ToList()
            };
        }

        // ─── Push notification label helpers ────────────────────────────────
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
            UserPahse.phaseone      => "I ეტაპი: გთხოვთ ატვირთოთ თქვენი რეზიუმე (CV).",
            UserPahse.phasetwo      => "II ეტაპი: ჩამოტვირთეთ ხელშეკრულება, შეავსეთ და ატვირთეთ PDF-ად.",
            UserPahse.phasethree    => "III ეტაპი: გადმოგეცემათ სამუშაო ნებართვა. ეწვიეთ კაბინეტს.",
            UserPahse.phaseCanceled => "თქვენი ეტაპი გაუქმებულია. კითხვებისთვის დაგვიკავშირდით.",
            _                       => "თქვენი ეტაპი განახლდა."
        };

        public async Task<Result<bool>> ToggleDocumentAdminUploadedAsync(int documentId)
        {
            var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == documentId);
            if (doc == null)
            {
                return Result<bool>.NotFound("Document not found");
            }

            doc.IsAdminUploaded = !doc.IsAdminUploaded;
            await _db.SaveChangesAsync();
            return Result<bool>.Ok(doc.IsAdminUploaded);
        }
    }
}
