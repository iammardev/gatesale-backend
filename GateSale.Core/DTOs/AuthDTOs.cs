using System.ComponentModel.DataAnnotations;

namespace GateSale.Core.DTOs
{
    public class RegisterDto
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }
        
        [Required]
        [MinLength(6)]
        public required string Password { get; set; }
        
        [Required]
        public required string FullName { get; set; }
        
        [Required]
        public required string School { get; set; }
        
        [Required]
        public DateTime DateOfBirth 
        { 
            get => _dateOfBirth;
            set => _dateOfBirth = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
        private DateTime _dateOfBirth;
        
        [Required]
        [Range(8, 12)]
        public int Grade { get; set; }
        
        public string? ParentEmail { get; set; }
    }
    
    public class LoginDto
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }
        
        [Required]
        public required string Password { get; set; }
    }
    
    public class VerifyEmailDto
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }
        
        [Required]
        public required string Code { get; set; }
    }
    
    public class ParentalConsentDto
    {
        [Required]
        public required string ConsentToken { get; set; }
        
        [Required]
        public bool ConsentGiven { get; set; }
    }
    
    public class ForgotPasswordRequestDto
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }
    }

    public class VerifyResetCodeDto
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        [StringLength(6, MinimumLength = 6)]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Code must be a 6-digit number")]
        public required string Code { get; set; }
    }

    public class ResetPasswordDto
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        [StringLength(6, MinimumLength = 6)]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Code must be a 6-digit number")]
        public required string Code { get; set; }

        [Required]
        [MinLength(6)]
        public required string NewPassword { get; set; }
    }
    
    public class AuthResponseDto
    {
        public required string Token { get; set; }
        public required string RefreshToken { get; set; }
        public DateTime ExpiresAt 
        { 
            get => _expiresAt;
            set => _expiresAt = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
        private DateTime _expiresAt;
        public string? UserId { get; set; }
        public required UserProfileDto User { get; set; }
    }
    
    public class UserProfileDto
    {
        public Guid Id { get; set; }
        public required string Email { get; set; }
        public required string FullName { get; set; }
        public required string School { get; set; }
        public string? ProfileImageUrl { get; set; }
        public bool IsEmailVerified { get; set; }
        public bool ParentalConsentGiven { get; set; }
        public bool IsProfileComplete { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}