using System;

namespace GateSale.Core.Entities
{
    public class UserLocker
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        
        public Guid LockerId { get; set; }
        public Locker Locker { get; set; } = null!;
        
        public bool IsFavorite { get; set; }
        public bool IsDefault { get; set; }
        public bool IsSellerDropoff { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastUsedAt { get; set; }
    }
}
