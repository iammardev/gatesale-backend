using GateSale.Core.Enums;

namespace GateSale.Core.Entities
{
    public class Order
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        public decimal TotalAmount { get; set; }
        public OrderStatus Status { get; set; } = OrderStatus.PaidAwaitingShipment;
        
        // Buyer Information
        public Guid BuyerId { get; set; }
        public User Buyer { get; set; } = null!;
        
        // Seller Information (derived from product)
        public Guid? SellerId { get; set; }
        public User? Seller { get; set; }
        
        // Pickup Location (Buyer's Pudo Locker)
        public Guid? BuyerLockerId { get; set; }
        public Locker? BuyerLocker { get; set; }
        
        // Dropoff Location (Seller's Pudo Locker)
        public Guid? SellerLockerId { get; set; }
        public Locker? SellerLocker { get; set; }
        
        // Shipping Details
        public string? PudoTrackingNumber { get; set; }
        public string? PudoShipmentReference { get; set; }
        public decimal ShippingCost { get; set; }
        public string? PackageSize { get; set; }
        
        // Return Shipping Details
        public string? ReturnTrackingNumber { get; set; }
        public string? ReturnShipmentReference { get; set; }
        public decimal? ReturnShippingCost { get; set; }
        public bool IsReturnPaidBySeller { get; set; }
        
        // Financial Details
        public decimal ItemSubtotal { get; set; }
        public decimal AdminFeePercentage { get; set; } = 10; // Default 10%
        public decimal AdminFeeAmount { get; set; }
        public decimal SellerPayoutAmount { get; set; }
        
        // Items
        public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
        
        // Payment
        public Transaction? Transaction { get; set; }
        
        // Dispute
        public Dispute? Dispute { get; set; }
        
        // Timestamps
        public DateTime? ShippedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime? CollectedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? DisputeInitiatedAt { get; set; }
        public DateTime? DisputeResolvedAt { get; set; }
        public DateTime? ReturnInitiatedAt { get; set; }
        public DateTime? ReturnCompletedAt { get; set; }
        public DateTime? RefundedAt { get; set; }
        public DateTime? SellerPaidAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
    }
}