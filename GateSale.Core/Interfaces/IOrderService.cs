using GateSale.Core.Entities;
using GateSale.Core.Enums;

namespace GateSale.Core.Interfaces
{
    public interface IOrderService
    {
        // Order Creation
        Task<Order> CreateOrderAsync(Guid productId, Guid buyerId, Guid buyerLockerId, decimal shippingCost);
        
        // Order Retrieval
        Task<Order?> GetOrderByIdAsync(Guid orderId);
        Task<IEnumerable<Order>> GetOrdersByBuyerIdAsync(Guid buyerId);
        Task<IEnumerable<Order>> GetOrdersBySellerIdAsync(Guid sellerId);
        
        // Order Status Management
        Task<bool> UpdateOrderStatusAsync(Guid orderId, OrderStatus newStatus, string? notes = null);
        
        // Shipping Management
        Task<bool> ProcessSellerShipmentAsync(Guid orderId, string pudoTrackingNumber, string pudoShipmentReference);
        Task<bool> MarkOrderDeliveredAsync(Guid orderId);
        Task<bool> MarkOrderCollectedAsync(Guid orderId);
        
        // Order Completion
        Task<bool> ApproveOrderByBuyerAsync(Guid orderId);
        Task<bool> CompleteOrderAsync(Guid orderId);
        
        // Order Cancellation
        Task<bool> CancelOrderBySellerAsync(Guid orderId, string reason, string? description = null);
        
        // Financial Management
        Task<bool> CalculateFeesAsync(Guid orderId);
        Task<bool> ProcessSellerPayoutAsync(Guid orderId);
        
        // Validation
        Task<bool> ValidateOrderForPaymentAsync(Guid productId, Guid buyerId);
        Task<bool> ValidateOrderForShipmentAsync(Guid orderId);
        
        // User Validation
        Task<bool> CheckUserExistsAsync(Guid userId);
    }
}
