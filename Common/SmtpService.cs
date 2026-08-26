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
                    var brevoKey = _configuration["Brevo:ApiKey"] 
                                ?? _configuration["BrevoApiKey"] 
                                ?? _configuration["Brevo__ApiKey"] 
                                ?? _configuration["BREVO_API_KEY"];

                    var resendKey = _configuration["Resend:ApiKey"] 
                                 ?? _configuration["ResendApiKey"] 
                                 ?? _configuration["Resend__ApiKey"] 
                                 ?? _configuration["RESEND_API_KEY"];

                    string htmlBody = $@"<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1.0' />
    <style>
        body {{ font-family: sans-serif; background: #0f172a; padding: 20px; color: #f8fafc; }}
        .container {{ background: #1e293b; border-radius: 16px; border: 1px solid #334155; padding: 32px; max-width: 500px; margin: 0 auto; }}
        .logo {{ font-size: 24px; font-weight: 800; color: #3b82f6; text-align: center; margin-bottom: 20px; }}
        .code-box {{ background: #090d16; border: 2px dashed #3b82f6; padding: 20px; border-radius: 12px; text-align: center; margin: 20px 0; }}
        .code-val {{ font-size: 32px; font-weight: 800; color: #60a5fa; letter-spacing: 6px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='logo'>GETO Project</div>
        <h2 style='color: #ffffff;'>{subject}</h2>
        <p style='color: #94a3b8;'>Use the verification code below to complete your account security check:</p>
        <div class='code-box'>
            <div class='code-val'>{code}</div>
        </div>
        <p style='color: #64748b; font-size: 12px;'>Valid for 10 minutes. If you did not request this code, please ignore.</p>
    </div>
</body>
</html>";

                    // 1. Try Brevo HTTP API (Sends to ANY email address for free!)
                    if (!string.IsNullOrEmpty(brevoKey) && !brevoKey.Contains("YOUR_"))
                    {
                        var brevoPayload = new
                        {
                            sender = new { name = "GETO Project", email = "cheshmaritashvilizaza@gmail.com" },
                            to = new[] { new { email = email } },
                            subject = subject,
                            htmlContent = htmlBody
                        };

                        var brevoRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
                        brevoRequest.Headers.Add("api-key", brevoKey.Trim());
                        brevoRequest.Content = new StringContent(JsonSerializer.Serialize(brevoPayload), Encoding.UTF8, "application/json");

                        _logger.LogInformation($"[Brevo API] Sending email to {email}...");
                        var brevoResponse = await _httpClient.SendAsync(brevoRequest);
                        var brevoResult = await brevoResponse.Content.ReadAsStringAsync();

                        if (brevoResponse.IsSuccessStatusCode)
                        {
                            _logger.LogInformation($"[Brevo API SUCCESS] Email delivered to {email}! Result: {brevoResult}");
                            return;
                        }
                        _logger.LogError($"[Brevo API Failed] HTTP {brevoResponse.StatusCode}: {brevoResult}");
                    }

                    // 2. Try Resend HTTP API
                    if (!string.IsNullOrEmpty(resendKey) && !resendKey.Contains("YOUR_"))
                    {
                        var resendPayload = new
                        {
                            from = "GETO Project <onboarding@resend.dev>",
                            to = new[] { email },
                            subject = subject,
                            html = htmlBody
                        };

                        var resendRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
                        resendRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", resendKey.Trim());
                        resendRequest.Content = new StringContent(JsonSerializer.Serialize(resendPayload), Encoding.UTF8, "application/json");

                        _logger.LogInformation($"[Resend API] Sending email to {email}...");
                        var resendResponse = await _httpClient.SendAsync(resendRequest);
                        var resendResult = await resendResponse.Content.ReadAsStringAsync();

                        if (resendResponse.IsSuccessStatusCode)
                        {
                            _logger.LogInformation($"[Resend API SUCCESS] Email delivered to {email}! Result: {resendResult}");
                            return;
                        }
                        _logger.LogError($"[Resend API Failed] HTTP {resendResponse.StatusCode}: {resendResult}");
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