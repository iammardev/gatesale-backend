using GateSale.Core.Enums;

namespace GateSale.Core.Entities
{
    public class EmailVerification
    {
        public Guid Id { get; set; }
        public required string Email { get; set; }
        public required string VerificationCode { get; set; }
        public VerificationType Type { get; set; }
        public bool IsUsed { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; }
        public DateTime? UsedAt { get; set; }
    }
}