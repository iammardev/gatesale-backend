using GateSale.Core.Enums;

namespace GateSale.Core.Entities
{
    public class Locker
    {
        public Guid Id { get; set; }
        public required string LockerCode { get; set; }
        public required string Location { get; set; }
        public string? Description { get; set; }
        public LockerStatus Status { get; set; } = LockerStatus.Available;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Relations
        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}