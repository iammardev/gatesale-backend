namespace GateSale.Core.Entities
{
    public class UserDevice
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        
        public required string DeviceToken { get; set; }
        public required string Platform { get; set; } // iOS, Android, Web
        public string? DeviceInfo { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastUsedAt { get; set; }
    }
}