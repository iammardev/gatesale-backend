using GateSale.Core.Entities;
using GateSale.Core.Enums;
using GateSale.Core.Interfaces;
using GateSale.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GateSale.Infrastructure.Services
{
    public class OrderService : IOrderService
    {
        private readonly GateSaleDbContext _dbContext;
        private readonly IOrderTrackingService _orderTrackingService;
        private readonly ILogger<OrderService> _logger;

        public OrderService(
            GateSaleDbContext dbContext,
            IOrderTrackingService orderTrackingService,
            ILogger<OrderService> logger)
        {
            _dbContext = dbContext;
            _orderTrackingService = orderTrackingService;
            _logger = logger;
        }

        public async Task<Order> CreateOrderAsync(Guid productId, Guid buyerId, Guid buyerLockerId, decimal shippingCost)
        {
            try
            {
                // Get the product
                var product = await _dbContext.Products
                    .Include(p => p.Seller)
                    .FirstOrDefaultAsync(p => p.Id == productId && p.Status == ProductStatus.Listed);

                if (product == null)
                {
                    throw new ArgumentException($"Product with ID {productId} not found or not available");
                }

                // Get the buyer locker
                var buyerLocker = await _dbContext.Lockers.FindAsync(buyerLockerId);
                if (buyerLocker == null)
                {
                    throw new ArgumentException($"Buyer locker with ID {buyerLockerId} not found");
                }

                // Calculate financial details
                var itemSubtotal = product.Price;
                var totalAmount = itemSubtotal + shippingCost;
                var adminFeePercentage = 10m; // 10%
                var adminFeeAmount = Math.Round(itemSubtotal * adminFeePercentage / 100, 2);
                var sellerPayoutAmount = itemSubtotal - adminFeeAmount;

                // Create the order
                var order = new Order
                {
                    OrderNumber = GenerateOrderNumber(),
                    OrderDate = DateTime.UtcNow,
                    TotalAmount = totalAmount,
                    Status = OrderStatus.PaidAwaitingShipment,
                    
                    // Buyer Information
                    BuyerId = buyerId,
                    
                    // Seller Information
                    SellerId = product.SellerId,
                    Seller = product.Seller,
                    
                    // Pickup Location (Buyer's Pudo Locker)
                    BuyerLockerId = buyerLockerId,
                    BuyerLocker = buyerLocker,
                    
                    // Shipping Details
                    ShippingCost = shippingCost,
                    
                    // Financial Details
                    ItemSubtotal = itemSubtotal,
                    AdminFeePercentage = adminFeePercentage,
                    AdminFeeAmount = adminFeeAmount,
                    SellerPayoutAmount = sellerPayoutAmount
                };

                // Add order item
                var orderItem = new OrderItem
                {
                    ProductId = productId,
                    Product = product,
                    Quantity = 1,
                    UnitPrice = product.Price,
                    TotalPrice = product.Price
                };

                order.Items.Add(orderItem);

                // Update product status
                product.Status = ProductStatus.Sold;

                // Save to database
                _dbContext.Orders.Add(order);
                await _dbContext.SaveChangesAsync();

                // Log order creation event
                await _orderTrackingService.LogOrderTrackingEvent(
                    order.Id,
                    "OrderCreated",
                    "Order created and payment received",
                    null);

                return order;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order for product {ProductId}", productId);
                throw;
            }
        }

        public async Task<Order?> GetOrderByIdAsync(Guid orderId)
        {
            return await _dbContext.Orders
                .Include(o => o.Buyer)
                .Include(o => o.Seller)
                .Include(o => o.BuyerLocker)
                .Include(o => o.SellerLocker)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                        .ThenInclude(p => p.Images)
                .Include(o => o.Dispute)
                .FirstOrDefaultAsync(o => o.Id == orderId);
        }

        public async Task<IEnumerable<Order>> GetOrdersByBuyerIdAsync(Guid buyerId)
        {
            return await _dbContext.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                        .ThenInclude(p => p.Images.Take(1))
                .Where(o => o.BuyerId == buyerId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<Order>> GetOrdersBySellerIdAsync(Guid sellerId)
        {
            return await _dbContext.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                        .ThenInclude(p => p.Images.Take(1))
                .Where(o => o.SellerId == sellerId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
        }

        public async Task<bool> UpdateOrderStatusAsync(Guid orderId, OrderStatus newStatus, string? notes = null)
        {
            try
            {
                var order = await _dbContext.Orders.FindAsync(orderId);
                if (order == null)
                {
                    return false;
                }

                var oldStatus = order.Status;
                order.Status = newStatus;

                // Update timestamps based on status
                UpdateOrderTimestamps(order, newStatus);

                await _dbContext.SaveChangesAsync();

                // Log status change
                await _orderTrackingService.LogOrderTrackingEvent(
                    orderId,
                    "StatusChange",
                    $"Order status changed from {oldStatus} to {newStatus}",
                    notes);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status for order {OrderId}", orderId);
                return false;
            }
        }

        public async Task<bool> ProcessSellerShipmentAsync(Guid orderId, string pudoTrackingNumber, string pudoShipmentReference)
        {
            try
            {
                var order = await _dbContext.Orders.FindAsync(orderId);
                if (order == null)
                {
                    return false;
                }

                // Update order with shipping details
                order.PudoTrackingNumber = pudoTrackingNumber;
                order.PudoShipmentReference = pudoShipmentReference;
                order.Status = OrderStatus.InTransit;
                order.ShippedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                // Log shipment event
                await _orderTrackingService.LogOrderTrackingEvent(
                    orderId,
                    "Shipped",
                    $"Order shipped with tracking number {pudoTrackingNumber}",
                    null);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing shipment for order {OrderId}", orderId);
                return false;
            }
        }

        public async Task<bool> MarkOrderDeliveredAsync(Guid orderId)
        {
            try
            {
                var order = await _dbContext.Orders.FindAsync(orderId);
                if (order == null)
                {
                    return false;
                }

                order.Status = OrderStatus.Delivered;
                order.DeliveredAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                // Log delivery event
                await _orderTrackingService.LogOrderTrackingEvent(
                    orderId,
                    "Delivered",
                    "Order delivered to Pudo locker",
                    null);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking order {OrderId} as delivered", orderId);
                return false;
            }
        }

        public async Task<bool> MarkOrderCollectedAsync(Guid orderId)
        {
            try
            {
                var order = await _dbContext.Orders.FindAsync(orderId);
                if (order == null)
                {
                    return false;
                }

                order.Status = OrderStatus.Collected;
                order.CollectedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                // Log collection event
                await _orderTrackingService.LogOrderTrackingEvent(
                    orderId,
                    "Collected",
                    "Order collected by buyer",
                    null);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking order {OrderId} as collected", orderId);
                return false;
            }
        }

        public async Task<bool> ApproveOrderByBuyerAsync(Guid orderId)
        {
            try
            {
                var order = await _dbContext.Orders.FindAsync(orderId);
                if (order == null)
                {
                    return false;
                }

                order.Status = OrderStatus.BuyerApproved;
                order.ApprovedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                // Log approval event
                await _orderTrackingService.LogOrderTrackingEvent(
                    orderId,
                    "BuyerApproved",
                    "Order approved by buyer",
                    null);

                // Process seller payout
                await ProcessSellerPayoutAsync(orderId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving order {OrderId} by buyer", orderId);
                return false;
            }
        }

        public async Task<bool> CompleteOrderAsync(Guid orderId)
        {
            try
            {
                var order = await _dbContext.Orders.FindAsync(orderId);
                if (order == null)
                {
                    return false;
                }

                order.Status = OrderStatus.Completed;
                order.CompletedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                // Log completion event
                await _orderTrackingService.LogOrderTrackingEvent(
                    orderId,
                    "Completed",
                    "Order completed",
                    null);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing order {OrderId}", orderId);
                return false;
            }
        }

        public async Task<bool> CancelOrderBySellerAsync(Guid orderId, string reason, string? description = null)
        {
            try
            {
                var order = await _dbContext.Orders.FindAsync(orderId);
                if (order == null)
                {
                    return false;
                }

                order.Status = OrderStatus.CancelledBySeller;
                order.CancelledAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                // Log cancellation event
                await _orderTrackingService.LogOrderTrackingEvent(
                    orderId,
                    "CancelledBySeller",
                    $"Order cancelled by seller. Reason: {reason}",
                    description);

                // TODO: Process refund to buyer

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling order {OrderId} by seller", orderId);
                return false;
            }
        }

        public async Task<bool> CalculateFeesAsync(Guid orderId)
        {
            try
            {
                var order = await _dbContext.Orders.FindAsync(orderId);
                if (order == null)
                {
                    return false;
                }

                // Calculate admin fee (10% of item subtotal)
                var adminFeePercentage = 10m;
                var adminFeeAmount = Math.Round(order.ItemSubtotal * adminFeePercentage / 100, 2);
                var sellerPayoutAmount = order.ItemSubtotal - adminFeeAmount;

                order.AdminFeePercentage = adminFeePercentage;
                order.AdminFeeAmount = adminFeeAmount;
                order.SellerPayoutAmount = sellerPayoutAmount;

                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating fees for order {OrderId}", orderId);
                return false;
            }
        }

        public async Task<bool> ProcessSellerPayoutAsync(Guid orderId)
        {
            try
            {
                var order = await _dbContext.Orders.FindAsync(orderId);
                if (order == null)
                {
                    return false;
                }

                // Set status to awaiting payout
                order.Status = OrderStatus.AwaitingPayout;

                // TODO: Integrate with payment gateway to process the actual payout
                // For now, just mark it as completed
                
                // Update order status to completed
                await CompleteOrderAsync(orderId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payout for order {OrderId}", orderId);
                return false;
            }
        }

        public async Task<bool> ValidateOrderForPaymentAsync(Guid productId, Guid buyerId)
        {
            try
            {
                // Check if product exists and is available
                var product = await _dbContext.Products
                    .FirstOrDefaultAsync(p => p.Id == productId);

                if (product == null || product.Status != ProductStatus.Listed)
                {
                    return false;
                }

                // Check if buyer is not the seller
                if (product.SellerId == buyerId)
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating order for payment. ProductId: {ProductId}, BuyerId: {BuyerId}", productId, buyerId);
                return false;
            }
        }

        public async Task<bool> ValidateOrderForShipmentAsync(Guid orderId)
        {
            try
            {
                var order = await _dbContext.Orders.FindAsync(orderId);
                if (order == null)
                {
                    return false;
                }

                // Check if order is in the correct state for shipment
                return order.Status == OrderStatus.PaidAwaitingShipment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating order {OrderId} for shipment", orderId);
                return false;
            }
        }
        
        public async Task<bool> CheckUserExistsAsync(Guid userId)
        {
            try
            {
                var user = await _dbContext.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User with ID {UserId} not found in database", userId);
                    return false;
                }
                
                _logger.LogInformation("User with ID {UserId} found in database. Username: {Username}", userId, user.Username);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user {UserId} exists", userId);
                return false;
            }
        }

        #region Helper Methods

        private string GenerateOrderNumber()
        {
            // Format: GSB-yyyyMMdd-XXXX (e.g., GSB-20250914-1234)
            var dateComponent = DateTime.UtcNow.ToString("yyyyMMdd");
            var randomComponent = new Random().Next(1000, 9999).ToString();
            return $"GSB-{dateComponent}-{randomComponent}";
        }

        private void UpdateOrderTimestamps(Order order, OrderStatus newStatus)
        {
            switch (newStatus)
            {
                case OrderStatus.InTransit:
                    order.ShippedAt = DateTime.UtcNow;
                    break;
                case OrderStatus.Delivered:
                    order.DeliveredAt = DateTime.UtcNow;
                    break;
                case OrderStatus.Collected:
                    order.CollectedAt = DateTime.UtcNow;
                    break;
                case OrderStatus.BuyerApproved:
                    order.ApprovedAt = DateTime.UtcNow;
                    break;
                case OrderStatus.DisputeInProgress:
                    order.DisputeInitiatedAt = DateTime.UtcNow;
                    break;
                case OrderStatus.Refunded:
                    order.RefundedAt = DateTime.UtcNow;
                    break;
                case OrderStatus.AwaitingPayout:
                    // Will be set when payout is processed
                    break;
                case OrderStatus.Completed:
                    order.CompletedAt = DateTime.UtcNow;
                    break;
                case OrderStatus.CancelledBySeller:
                    order.CancelledAt = DateTime.UtcNow;
                    break;
            }
        }

        #endregion
    }
}
