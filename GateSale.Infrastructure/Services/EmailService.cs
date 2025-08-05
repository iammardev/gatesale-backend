using GateSale.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;

namespace GateSale.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly string _fromEmail;
        private readonly string _smtpServer;
        private readonly int _smtpPort;
        private readonly string _smtpUsername;
        private readonly string _smtpPassword;
        private readonly bool _enableActualEmailSending;
        private readonly string _environment;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            
            // _fromEmail = _configuration["EmailSettings:SenderEmail"] ?? "noreply@gatesale.com";
            // _smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "smtp.example.com";
            // _smtpPort = int.Parse(_configuration["EmailSettings:Port"] ?? "587");
            // _smtpUsername = _configuration["EmailSettings:Username"] ?? "user";
            // _smtpPassword = _configuration["EmailSettings:Password"] ?? "password";
            _fromEmail = _configuration["GoogleSMTP:Username"]; // Gmail email
            _smtpServer = _configuration["GoogleSMTP:SmtpServer"];
            _smtpPort = int.Parse(_configuration["GoogleSMTP:Port"]);
            _smtpUsername = _configuration["GoogleSMTP:Username"];
            _smtpPassword = _configuration["GoogleSMTP:Password"];
            
            // Check if we're in development mode to avoid email errors during development
            _environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development";
            _enableActualEmailSending = bool.TryParse(_configuration["EmailSettings:EnableActualSending"], out var enabled) ? enabled : false;
            
            _logger.LogInformation($"Email service initialized in {_environment} mode. Actual sending: {_enableActualEmailSending}");
        }

        public async Task SendVerificationEmailAsync(string email, string token, string callbackUrl)
        {
            var subject = "Verify your GateSale account";
            var body = $@"
                <h2>Welcome to GateSale!</h2>
                <p>Please verify your email address by clicking the link below:</p>
                <p><a href='{callbackUrl}?token={WebUtility.UrlEncode(token)}'>Verify Email Address</a></p>
                <p>If you did not create this account, please ignore this email.</p>
                <p>Thank you,<br>The GateSale Team</p>
            ";

            await SendEmailAsync(email, subject, body);
        }

        public async Task SendParentalConsentEmailAsync(string parentEmail, string parentName, string studentName, string consentToken, string callbackUrl)
        {
            var subject = $"Parental Consent Request for {studentName}'s GateSale Account";
            var body = $@"
                <h2>Hi {parentName},</h2>
                <p>Your child, {studentName}, has signed up to use GateSale — a safe online marketplace exclusively for school learners.</p>
                <p>To protect all users, we require parental or guardian consent before allowing access to post or interact on the platform.</p>
                
                <h3>What is GateSale?</h3>
                <p>GateSale lets students buy and sell items like textbooks, uniforms, and electronics within their school community, in a secure, moderated environment.</p>
                <p>All users are verified students, and content is pre-screened for safety.</p>
                
                <h3>What You're Approving</h3>
                <p>By clicking the link below, you're giving permission for your child to use GateSale under the following conditions:</p>
                <ul>
                    <li>They are a current school learner.</li>
                    <li>They will behave responsibly and follow community guidelines.</li>
                    <li>You understand GateSale is monitored for safety, but is not liable for off-platform transactions.</li>
                </ul>
                
                <p><a href='{callbackUrl}?token={WebUtility.UrlEncode(consentToken)}'>Approve Access Now</a></p>
                
                <p>If you do not recognize this request, simply ignore this email and no account will be activated.</p>
                <p>Thank you for helping us keep GateSale a safe place for students.</p>
                
                <p>Warm regards,<br>The GateSale Team</p>
                <p><a href='https://gatesale.com'>Website</a> | <a href='mailto:support@gatesale.com'>Support Email</a></p>
            ";

            await SendEmailAsync(parentEmail, subject, body);
            
            // For testing - log the consent URL even if email fails
            _logger.LogInformation("Parental consent URL: {0}?token={1}", callbackUrl, consentToken);
        }

        public async Task SendPasswordResetEmailAsync(string email, string token, string callbackUrl)
        {
            var subject = "Reset your GateSale password";
            var body = $@"
                <h2>Password Reset Request</h2>
                <p>We received a request to reset your password. Click the link below to set a new password:</p>
                <p><a href='{callbackUrl}?token={WebUtility.UrlEncode(token)}'>Reset Password</a></p>
                <p>If you did not request a password reset, please ignore this email.</p>
                <p>Thank you,<br>The GateSale Team</p>
            ";

            await SendEmailAsync(email, subject, body);
        }

        private async Task SendEmailAsync(string to, string subject, string htmlBody)
        {
            // Always log the email content during development for debugging
            if (_environment.Equals("Development", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Email to: {0}, Subject: {1}, Body: {2}", to, subject, htmlBody);
            }
            
            // If not in production or email sending is disabled, don't actually send
            if (!_enableActualEmailSending && _environment.Equals("Development", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Email sending suppressed in development mode. Would have sent email to {0}", to);
                return;
            }
            
            try
            {
                using var client = new SmtpClient(_smtpServer, _smtpPort);
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);
                client.EnableSsl = true;

                using var message = new MailMessage();
                message.From = new MailAddress(_fromEmail, "GateSale");
                message.Subject = subject;
                message.Body = htmlBody;
                message.IsBodyHtml = true;
                message.To.Add(to);

                await client.SendMailAsync(message);
                _logger.LogInformation($"Email sent successfully to {to}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email to {to}");
                
                // Don't throw the exception in development mode
                if (!_environment.Equals("Development", StringComparison.OrdinalIgnoreCase))
                {
                    throw;
                }
            }
        }
    }
} 