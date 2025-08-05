namespace GateSale.Core.Enums
{
    public enum UserStatus
    {
        Pending = 0,
        PendingEmailVerification = 1,
        PendingParentalConsent = 2,
        Active = 3,
        ReadOnly = 4,
        Suspended = 5,
        Banned = 6
    }
    
    public enum VerificationType
    {
        EmailVerification = 0,
        PasswordReset = 1,
        TwoFactorAuth = 2,
        ParentalConsent = 3
    }
}