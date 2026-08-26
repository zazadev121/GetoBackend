using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace apiprojnew.Common
{
    public class SmtpService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SmtpService> _logger;

        public SmtpService(IConfiguration configuration, ILogger<SmtpService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public void SendEmailAsync(string subject, string body, string email)
        {
            // Fire and forget in a background thread so the HTTP request returns instantly!
            Task.Run(async () =>
            {
                try
                {
                    var senderEmail = _configuration["Smtp:Email"];
                    var senderPassword = _configuration["Smtp:Password"];

                    if (string.IsNullOrEmpty(senderEmail) || string.IsNullOrEmpty(senderPassword))
                    {
                        _logger.LogError("[SMTP Error] Smtp:Email or Smtp:Password environment variables are missing!");
                        return;
                    }

                    string htmlBody = $@"<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1.0' />
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ 
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; 
            background: #0f172a;
            padding: 20px;
            color: #f8fafc;
        }}
        .wrapper {{ max-width: 600px; margin: 0 auto; }}
        .container {{ background: #1e293b; border-radius: 16px; border: 1px solid #334155; overflow: hidden; padding: 32px; }}
        .header {{ text-align: center; margin-bottom: 24px; }}
        .logo {{ font-size: 28px; font-weight: 800; color: #3b82f6; }}
        .content h1 {{ font-size: 20px; font-weight: 700; color: #ffffff; margin-bottom: 12px; }}
        .content p {{ font-size: 14px; color: #94a3b8; line-height: 1.6; margin-bottom: 20px; }}
        .code-box {{ background: #090d16; border: 2px dashed #3b82f6; padding: 20px; border-radius: 12px; text-align: center; margin: 24px 0; }}
        .code-val {{ font-size: 32px; font-weight: 800; color: #60a5fa; letter-spacing: 6px; font-mono: monospace; }}
        .footer {{ text-align: center; font-size: 12px; color: #64748b; margin-top: 24px; border-top: 1px solid #334155; padding-top: 16px; }}
    </style>
</head>
<body>
    <div class='wrapper'>
        <div class='container'>
            <div class='header'>
                <div class='logo'>GETO Project</div>
            </div>
            <div class='content'>
                <h1>{subject}</h1>
                <p>Use the 6-digit verification code below to verify your email address and activate your GETO Portal account:</p>
                <div class='code-box'>
                    <div class='code-val'>{body.Replace("Code : ", "").Replace("Your verification code is: ", "")}</div>
                </div>
                <p>This verification code is valid for 10 minutes. If you did not request this code, please ignore this message.</p>
            </div>
            <div class='footer'>
                <p>&copy; 2026 GETO Project. All rights reserved.</p>
            </div>
        </div>
    </div>
</body>
</html>";

                    using var mail = new MailMessage
                    {
                        From = new MailAddress(senderEmail, "GETO Project"),
                        Subject = subject,
                        Body = htmlBody,
                        IsBodyHtml = true
                    };
                    mail.To.Add(email);

                    using var smtpClient = new SmtpClient("smtp.gmail.com")
                    {
                        Port = 587,
                        EnableSsl = true,
                        DeliveryMethod = SmtpDeliveryMethod.Network,
                        UseDefaultCredentials = false,
                        Credentials = new NetworkCredential(senderEmail, senderPassword),
                        Timeout = 10000 // 10 second timeout max
                    };

                    _logger.LogInformation($"[SMTP] Sending email '{subject}' to {email} via smtp.gmail.com:587...");
                    await smtpClient.SendMailAsync(mail);
                    _logger.LogInformation($"[SMTP] Successfully sent email to {email}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"[SMTP Exception] Failed to send email to {email}: {ex.Message}");
                }
            });
        }
    }
}