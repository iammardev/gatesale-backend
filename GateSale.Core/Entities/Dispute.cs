using GateSale.Core.Enums;

namespace GateSale.Core.Entities
{
    public class Dispute
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public Order Order { get; set; } = null!;
        
        public required string Reason { get; set; }
        public string? Description { get; set; }
        public DisputeStatus Status { get; set; } = DisputeStatus.Open;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }
    }
}