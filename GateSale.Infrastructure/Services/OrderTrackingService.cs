using GateSale.Core.Entities;
using GateSale.Core.Enums;
using GateSale.Core.Interfaces;
using GateSale.Core.Models;
using GateSale.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GateSale.Infrastructure.Services
{
    public class OrderTrackingService : IOrderTrackingService
    {
        private readonly GateSaleDbContext _dbContext;
        private readonly ILogger<OrderTrackingService> _logger;

        public OrderTrackingService(
            GateSaleDbContext dbContext,
            ILogger<OrderTrackingService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<OrderTrackingInfo> GetOrderTrackingInfo(Guid orderId)
        {
            var order = await _dbContext.Orders
                .Include(o => o.Locker)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                throw new ArgumentException($"Order with ID {orderId} not found");
            }

            var events = await _dbContext.OrderTrackingEvents
                .Where(e => e.OrderId == orderId)
                .OrderByDescending(e => e.Timestamp)
                .ToListAsync();

            var trackingInfo = new OrderTrackingInfo
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                Status = order.Status,
                OrderDate = order.OrderDate,
                CompletedAt = order.CompletedAt,
                CancelledAt = order.CancelledAt,
                Events = events.Select(e => new Core.Models.OrderTrackingEvent
                {
                    EventType = e.EventType,
                    Description = e.Description,
                    Timestamp = e.Timestamp,
                    Location = e.Location
                }).ToList()
            };

            if (order.Locker != null)
            {
                trackingInfo.Locker = new LockerInfo
                {
                    LockerCode = order.Locker.LockerCode,
                    Location = order.Locker.Location,
                    Description = order.Locker.Description,
                    Status = order.Locker.Status,
                    Latitude = order.Locker.Latitude,
                    Longitude = order.Locker.Longitude
                };
            }

            return trackingInfo;
        }

        public async Task<LockerInfo?> GetOrderLockerStatus(Guid orderId)
        {
            var order = await _dbContext.Orders
                .Include(o => o.Locker)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null || order.Locker == null)
            {
                return null;
            }

            return new LockerInfo
            {
                LockerCode = order.Locker.LockerCode,
                Location = order.Locker.Location,
                Description = order.Locker.Description,
                Status = order.Locker.Status,
                Latitude = order.Locker.Latitude,
                Longitude = order.Locker.Longitude
            };
        }

        public async Task<bool> UpdateOrderStatus(Guid orderId, OrderStatus newStatus, string? notes = null)
        {
            var order = await _dbContext.Orders.FindAsync(orderId);
            if (order == null)
            {
                return false;
            }

            var oldStatus = order.Status;
            order.Status = newStatus;

            // Update timestamps based on status
            if (newStatus == OrderStatus.Completed && !order.CompletedAt.HasValue)
            {
                order.CompletedAt = DateTime.UtcNow;
            }
            else if (newStatus == OrderStatus.Cancelled && !order.CancelledAt.HasValue)
            {
                order.CancelledAt = DateTime.UtcNow;
            }

            // Log the status change as an event
            var trackingEvent = new Core.Entities.OrderTrackingEvent
            {
                OrderId = orderId,
                EventType = "StatusChange",
                Description = $"Order status changed from {oldStatus} to {newStatus}",
                Timestamp = DateTime.UtcNow,
                Notes = notes
            };

            _dbContext.OrderTrackingEvents.Add(trackingEvent);
            await _dbContext.SaveChangesAsync();

            return true;
        }

        public async Task<bool> LogOrderTrackingEvent(Guid orderId, string eventType, string description, string? location = null)
        {
            var order = await _dbContext.Orders.FindAsync(orderId);
            if (order == null)
            {
                return false;
            }

            var trackingEvent = new Core.Entities.OrderTrackingEvent
            {
                OrderId = orderId,
                EventType = eventType,
                Description = description,
                Timestamp = DateTime.UtcNow,
                Location = location
            };

            _dbContext.OrderTrackingEvents.Add(trackingEvent);
            await _dbContext.SaveChangesAsync();

            return true;
        }
    }
}
