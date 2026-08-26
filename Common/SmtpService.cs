using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace apiprojnew.Common
{
    public class SmtpService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SmtpService> _logger;
        private static readonly HttpClient _httpClient = new HttpClient();

        public SmtpService(IConfiguration configuration, ILogger<SmtpService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public void SendEmailAsync(string subject, string body, string email)
        {
            string code = body.Replace("Code : ", "").Replace("Your verification code is: ", "").Trim();
            
            _logger.LogInformation($"[VERIFICATION CODE FOR {email}]: {code}");
            Console.WriteLine($"[VERIFICATION CODE FOR {email}]: {code}");

            // Fire and forget over HTTPS Port 443
            Task.Run(async () =>
            {
                try
                {
                    var emailJsServiceId = _configuration["EmailJs:ServiceId"] 
                                       ?? _configuration["EmailJsServiceId"] 
                                       ?? _configuration["EmailJs__ServiceId"] 
                                       ?? "service_oejpd6v";

                    var emailJsTemplateId = _configuration["EmailJs:TemplateId"] 
                                        ?? _configuration["EmailJsTemplateId"] 
                                        ?? _configuration["EmailJs__TemplateId"]
                                        ?? "template_5yp4o4e";

                    var emailJsPublicKey = _configuration["EmailJs:PublicKey"] 
                                       ?? _configuration["EmailJsPublicKey"] 
                                       ?? _configuration["EmailJs__PublicKey"]
                                       ?? "QQWzdMHl281Ejhe-A";

                    // 1. Try EmailJS API if PublicKey is provided
                    if (!string.IsNullOrEmpty(emailJsPublicKey))
                    {
                        var emailJsPayload = new
                        {
                            service_id = emailJsServiceId.Trim(),
                            template_id = emailJsTemplateId.Trim(),
                            user_id = emailJsPublicKey.Trim(),
                            template_params = new
                            {
                                to_email = email,
                                recipient = email,
                                code = code,
                                passcode = code,
                                time = "10 minutes",
                                subject = subject
                            }
                        };

                        var emailJsRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.emailjs.com/api/v1.0/email/send");
                        emailJsRequest.Content = new StringContent(JsonSerializer.Serialize(emailJsPayload), Encoding.UTF8, "application/json");

                        _logger.LogInformation($"[EmailJS API] Sending email via service '{emailJsServiceId}' to {email}...");
                        var emailJsResponse = await _httpClient.SendAsync(emailJsRequest);
                        var emailJsResult = await emailJsResponse.Content.ReadAsStringAsync();

                        if (emailJsResponse.IsSuccessStatusCode)
                        {
                            _logger.LogInformation($"[EmailJS SUCCESS] Real email sent to {email}! Result: {emailJsResult}");
                            return;
                        }
                        _logger.LogError($"[EmailJS Failed] HTTP {emailJsResponse.StatusCode}: {emailJsResult}");
                    }

                    // 2. Fallback: Check Brevo API
                    var brevoKey = _configuration["Brevo:ApiKey"] ?? _configuration["BrevoApiKey"] ?? _configuration["Brevo__ApiKey"];
                    if (!string.IsNullOrEmpty(brevoKey) && !brevoKey.Contains("YOUR_"))
                    {
                        var brevoPayload = new
                        {
                            sender = new { name = "GETO Project", email = "cheshmaritashvilizaza@gmail.com" },
                            to = new[] { new { email = email } },
                            subject = subject,
                            htmlContent = $"<h1>{subject}</h1><p>Your verification code is: <strong>{code}</strong></p>"
                        };

                        var brevoRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
                        brevoRequest.Headers.Add("api-key", brevoKey.Trim());
                        brevoRequest.Content = new StringContent(JsonSerializer.Serialize(brevoPayload), Encoding.UTF8, "application/json");

                        var brevoResponse = await _httpClient.SendAsync(brevoRequest);
                        if (brevoResponse.IsSuccessStatusCode) return;
                    }

                    // 3. Fallback: Check Resend API
                    var resendKey = _configuration["Resend:ApiKey"] ?? _configuration["ResendApiKey"] ?? _configuration["Resend__ApiKey"];
                    if (!string.IsNullOrEmpty(resendKey) && !resendKey.Contains("YOUR_"))
                    {
                        var resendPayload = new
                        {
                            from = "GETO Project <onboarding@resend.dev>",
                            to = new[] { email },
                            subject = subject,
                            html = $"<h1>{subject}</h1><p>Your verification code is: <strong>{code}</strong></p>"
                        };

                        var resendRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
                        resendRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", resendKey.Trim());
                        resendRequest.Content = new StringContent(JsonSerializer.Serialize(resendPayload), Encoding.UTF8, "application/json");

                        await _httpClient.SendAsync(resendRequest);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"[Email Exception] Error sending email to {email}: {ex.Message}");
                }
            });
        }
    }
}