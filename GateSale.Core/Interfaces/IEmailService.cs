namespace GateSale.Core.Interfaces
{
    public interface IEmailService
    {
        Task SendVerificationEmailAsync(string email, string token, string callbackUrl);
        Task SendParentalConsentEmailAsync(string parentEmail, string parentName, string studentName, string consentToken, string callbackUrl);
        Task SendPasswordResetEmailAsync(string email, string token, string callbackUrl);
    }
} 