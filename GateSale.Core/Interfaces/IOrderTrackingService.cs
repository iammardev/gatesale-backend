using GateSale.Core.Entities;
using GateSale.Core.Enums;
using GateSale.Core.Models;

namespace GateSale.Core.Interfaces
{
    public interface IOrderTrackingService
    {
        Task<OrderTrackingInfo> GetOrderTrackingInfo(Guid orderId);
        Task<LockerInfo?> GetOrderLockerStatus(Guid orderId);
        Task<bool> UpdateOrderStatus(Guid orderId, OrderStatus newStatus, string? notes = null);
        Task<bool> LogOrderTrackingEvent(Guid orderId, string eventType, string description, string? location = null);
    }
}
