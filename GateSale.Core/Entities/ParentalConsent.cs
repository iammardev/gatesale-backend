namespace GateSale.Core.Entities
{
    public class ParentalConsent
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        
        public required string ParentEmail { get; set; }
        public required string ConsentToken { get; set; }
        public bool IsConsentGiven { get; set; }
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ConsentGivenAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}