using GateSale.Core.Enums;

namespace GateSale.Core.Entities
{
    public class Order
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        public decimal TotalAmount { get; set; }
        public OrderStatus Status { get; set; } = OrderStatus.Pending;
        
        // Buyer Information
        public Guid BuyerId { get; set; }
        public User Buyer { get; set; } = null!;
        
        // Pickup Location
        public Guid? LockerId { get; set; }
        public Locker? Locker { get; set; }
        
        // Items
        public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
        
        // Payment
        public Transaction? Transaction { get; set; }
        
        // Dispute
        public Dispute? Dispute { get; set; }
        
        public DateTime? CompletedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
    }
}