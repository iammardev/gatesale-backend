using GateSale.Core.Enums;

namespace GateSale.Core.DTOs
{
    // Request DTOs
    
    public class CreateOrderRequest
    {
        public required Guid ProductId { get; set; }
        public required Guid BuyerLockerId { get; set; }
        public required decimal ShippingCost { get; set; }
        public string? PackageSize { get; set; }
    }
    
    public class UpdateOrderStatusRequest
    {
        public required OrderStatus Status { get; set; }
        public string? Notes { get; set; }
    }
    
    public class ProcessShipmentRequest
    {
        public required string PudoTrackingNumber { get; set; }
        public required string PudoShipmentReference { get; set; }
    }
    
    public class CancelOrderRequest
    {
        public required string Reason { get; set; }
        public string? Description { get; set; }
    }
    
    // Response DTOs
    
    public class OrderSummaryDto
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public OrderStatus Status { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal ShippingCost { get; set; }
        public string? ProductTitle { get; set; }
        public string? ProductImageUrl { get; set; }
        public Guid? SellerLockerId { get; set; }
        public Guid? BuyerLockerId { get; set; }
    }
    
    public class OrderDetailDto
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public OrderStatus Status { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        
        // Financial Details
        public decimal ItemSubtotal { get; set; }
        public decimal ShippingCost { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AdminFeeAmount { get; set; }
        public decimal SellerPayoutAmount { get; set; }
        
        // Product Details
        public Guid ProductId { get; set; }
        public string ProductTitle { get; set; } = string.Empty;
        public string? ProductDescription { get; set; }
        public string? ProductImageUrl { get; set; }
        public string Category { get; set; } = string.Empty;
        public string? SubCategory { get; set; }
        
        // Shipping Details
        public string? PudoTrackingNumber { get; set; }
        public string? PudoShipmentReference { get; set; }
        public string? PackageSize { get; set; }
        
        // Locker Details
        public LockerDto? BuyerLocker { get; set; }
        public LockerDto? SellerLocker { get; set; }
        
        // Timestamps
        public DateTime? ShippedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime? CollectedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        
        // Return Details (if applicable)
        public bool HasDispute { get; set; }
        public DisputeDto? Dispute { get; set; }
    }
    
    public class LockerDto
    {
        public Guid Id { get; set; }
        public string LockerCode { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string? Description { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
    
    public class DisputeDto
    {
        public Guid Id { get; set; }
        public DisputeReason ReasonCode { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DisputeStatus Status { get; set; }
        public bool IsReturnRequested { get; set; }
        public bool IsReturnPaidBySeller { get; set; }
        public DateTime CreatedAt { get; set; }
    }
    
    // Payment DTOs
    
    public class OrderPaymentInfoDto
    {
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public decimal ItemSubtotal { get; set; }
        public decimal ShippingCost { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentUrl { get; set; } = string.Empty;
        public string PaymentReference { get; set; } = string.Empty;
    }
}
