namespace GateSale.Core.Entities
{
    public class OrderTrackingEvent
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public Order Order { get; set; } = null!;
        
        public string EventType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? Location { get; set; }
        public string? Notes { get; set; }
    }
}
