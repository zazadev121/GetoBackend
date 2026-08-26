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
            // Extract code for backup logging
            string code = body.Replace("Code : ", "").Replace("Your verification code is: ", "").Trim();
            
            _logger.LogInformation($"[VERIFICATION CODE FOR {email}]: {code}");
            Console.WriteLine($"[VERIFICATION CODE FOR {email}]: {code}");

            // Fire and forget over HTTPS (Port 443 - Never blocked by Render!)
            Task.Run(async () =>
            {
                try
                {
                    // Check for Resend API Key in environment or config
                    var apiKey = _configuration["Resend:ApiKey"] ?? _configuration["ResendApiKey"];

                    if (string.IsNullOrEmpty(apiKey) || apiKey.Contains("YOUR_"))
                    {
                        _logger.LogWarning($"[Resend API] No Resend API Key configured yet. Verification code is logged above.");
                        return;
                    }

                    var payload = new
                    {
                        from = "GETO Project <onboarding@resend.dev>",
                        to = new[] { email },
                        subject = subject,
                        html = $@"<!DOCTYPE html>
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
        <p style='color: #64748b; font-size: 12px;'>Valid for 10 minutes. If you did not request this, please ignore.</p>
    </div>
</body>
</html>"
                    };

                    var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
                    requestMessage.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                    _logger.LogInformation($"[Resend API] Sending email '{subject}' to {email} via Resend HTTPS API...");
                    var response = await _httpClient.SendAsync(requestMessage);
                    var responseBody = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation($"[Resend API SUCCESS] Email delivered to {email}! Response: {responseBody}");
                    }
                    else
                    {
                        _logger.LogError($"[Resend API Failed] HTTP {response.StatusCode}: {responseBody}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"[Resend API Exception] Error sending email to {email}: {ex.Message}");
                }
            });
        }
    }
}