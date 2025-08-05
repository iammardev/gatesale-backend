using GateSale.Core.Enums;

namespace GateSale.Core.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public required string Username { get; set; }
        public required string Email { get; set; }
        public required string FullName { get; set; }
        public required string School { get; set; }
        public int Grade { get; set; }
        public string? ProfileImageUrl { get; set; }
        public DateTime DateOfBirth { get; set; }
        public bool IsMinor { get; set; }
        public string? ParentEmail { get; set; }
        public bool ParentalConsentGiven { get; set; }
        public DateTime? ParentalConsentDate { get; set; }
        public bool IsEmailVerified { get; set; }
        public bool IsProfileComplete { get; set; }
        public UserStatus Status { get; set; } = UserStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
        public string? PhoneNumber { get; set; }
        
        // Cognito specific properties
        public string CognitoUserId { get; set; } = string.Empty;
        public bool IsCognitoAccount { get; set; } = true;
        
        // Navigation Properties
        public ICollection<Product> Products { get; set; } = new List<Product>();
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ICollection<UserDevice> Devices { get; set; } = new List<UserDevice>();
    }
}