using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace apiprojnew.Common
{
    public class SmtpService
    {
        private readonly IConfiguration _configuration;

        public SmtpService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void SendEmail(string subject, string body, string email)
        {
            var senderEmail = _configuration["Smtp:Email"];
            var senderPassword = _configuration["Smtp:Password"];

            string htmlBody = $@"<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1.0' />
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ 
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; 
            background: linear-gradient(135deg, #f5f7fa 0%, #c3cfe2 100%);
            padding: 20px;
            min-height: 100vh;
        }}
        .wrapper {{ 
            max-width: 600px; 
            margin: 0 auto;
        }}
        .container {{ 
            background: #ffffff; 
            border-radius: 12px;
            box-shadow: 0 10px 40px rgba(0, 0, 0, 0.1);
            overflow: hidden;
        }}
        .header {{ 
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            padding: 40px 30px;
            text-align: center;
            color: #ffffff;
        }}
        .logo {{ 
            font-size: 28px; 
            font-weight: 700; 
            letter-spacing: -0.5px;
            margin-bottom: 8px;
        }}
        .tagline {{ 
            font-size: 14px; 
            opacity: 0.9;
            font-weight: 300;
            letter-spacing: 1px;
        }}
        .content {{ 
            padding: 40px 30px;
            color: #2d3748;
        }}
        .content h1 {{ 
            font-size: 24px; 
            font-weight: 600;
            margin-bottom: 16px;
            color: #1a202c;
        }}
        .content p {{ 
            font-size: 15px;
            line-height: 1.6;
            color: #4a5568;
            margin-bottom: 24px;
        }}
        .code-section {{ 
            background: #f7fafc;
            border-left: 4px solid #667eea;
            padding: 24px;
            border-radius: 8px;
            margin: 30px 0;
            text-align: center;
        }}
        .code-label {{ 
            font-size: 12px;
            font-weight: 600;
            text-transform: uppercase;
            letter-spacing: 1.5px;
            color: #718096;
            margin-bottom: 12px;
            display: block;
        }}
        .code-value {{ 
            font-size: 36px;
            font-weight: 700;
            color: #667eea;
            letter-spacing: 4px;
            font-family: 'Courier New', monospace;
        }}
        .footer-text {{ 
            font-size: 13px;
            color: #718096;
            line-height: 1.6;
            margin-top: 24px;
            padding-top: 24px;
            border-top: 1px solid #e2e8f0;
        }}
        .footer {{ 
            padding: 24px 30px;
            background: #f7fafc;
            border-top: 1px solid #e2e8f0;
            text-align: center;
            font-size: 12px;
            color: #a0aec0;
        }}
        .divider {{ 
            height: 1px; 
            background: #e2e8f0; 
            margin: 20px 0;
        }}
        @media only screen and (max-width: 600px) {{ 
            .container {{ border-radius: 8px; }}
            .header {{ padding: 30px 20px; }}
            .content {{ padding: 30px 20px; }}
            .content h1 {{ font-size: 20px; }}
            .code-value {{ font-size: 28px; letter-spacing: 2px; }}
            .footer {{ padding: 20px; }}
        }}
    </style>
</head>
<body>
    <div class='wrapper'>
        <div class='container'>
            <div class='header'>
                <div class='logo'>Geto Project</div>
                <div class='tagline'>Account Security</div>
            </div>
            
            <div class='content'>
                <h1>{subject}</h1>
                <p>Hi there,</p>
                <p>We're excited to have you on board! To complete your registration and secure your account, please use the verification code below.</p>
                
                <div class='code-section'>
                    <span class='code-label'>Your Verification Code</span>
                    <div class='code-value'>{body.Replace("Code : ", "").Replace("Your verification code is: ", "")}</div>
                </div>
                
                <p>This code will expire in 10 minutes. If you didn't request this verification, please ignore this email.</p>
                
                <div class='footer-text'>
                    <strong>Need help?</strong> If you have any questions, feel free to reach out to our support team.
                </div>
            </div>
            
            <div class='footer'>
                <p>© 2026 Geto Project. All rights reserved.<br>
                This is an automated message, please don't reply directly to this email.</p>
            </div>
        </div>
    </div>
</body>
</html>";

            using var mail = new MailMessage();
            mail.From = new MailAddress(senderEmail, "Geto Project");
            mail.IsBodyHtml = true;
            mail.Subject = subject;
            mail.To.Add(email);
            mail.Body = htmlBody;

            using var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                EnableSsl = true,
                Credentials = new NetworkCredential(senderEmail, senderPassword),
            };

            smtpClient.Send(mail);
        }
    }
}