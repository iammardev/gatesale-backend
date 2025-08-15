using GateSale.Core.Entities;
using GateSale.Core.Interfaces;
using GateSale.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GateSale.Infrastructure.Services
{
    public class UserLockerService : IUserLockerService
    {
        private readonly GateSaleDbContext _dbContext;
        private readonly IPudoLockerService _pudoLockerService;
        private readonly ILogger<UserLockerService> _logger;

        public UserLockerService(
            GateSaleDbContext dbContext,
            IPudoLockerService pudoLockerService,
            ILogger<UserLockerService> logger)
        {
            _dbContext = dbContext;
            _pudoLockerService = pudoLockerService;
            _logger = logger;
        }

        public async Task<IEnumerable<UserLocker>> GetUserFavoriteLockers(Guid userId)
        {
            return await _dbContext.UserLockers
                .Include(ul => ul.Locker)
                .Where(ul => ul.UserId == userId && ul.IsFavorite)
                .OrderByDescending(ul => ul.LastUsedAt)
                .ThenBy(ul => ul.CreatedAt)
                .ToListAsync();
        }

        public async Task<UserLocker?> GetUserDefaultLocker(Guid userId)
        {
            return await _dbContext.UserLockers
                .Include(ul => ul.Locker)
                .FirstOrDefaultAsync(ul => ul.UserId == userId && ul.IsDefault);
        }

        public async Task<IEnumerable<UserLocker>> GetSellerDropoffLockers(Guid sellerId)
        {
            return await _dbContext.UserLockers
                .Include(ul => ul.Locker)
                .Where(ul => ul.UserId == sellerId && ul.IsSellerDropoff)
                .OrderByDescending(ul => ul.LastUsedAt)
                .ThenBy(ul => ul.CreatedAt)
                .ToListAsync();
        }

        public async Task<UserLocker> AddFavoriteLocker(Guid userId, Guid lockerId)
        {
            var existingUserLocker = await _dbContext.UserLockers
                .FirstOrDefaultAsync(ul => ul.UserId == userId && ul.LockerId == lockerId);

            if (existingUserLocker != null)
            {
                existingUserLocker.IsFavorite = true;
                existingUserLocker.LastUsedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                return existingUserLocker;
            }

            var locker = await _dbContext.Lockers.FindAsync(lockerId);
            if (locker == null)
            {
                throw new ArgumentException($"Locker with ID {lockerId} not found");
            }

            var userLocker = new UserLocker
            {
                UserId = userId,
                LockerId = lockerId,
                IsFavorite = true,
                IsDefault = false,
                IsSellerDropoff = false,
                CreatedAt = DateTime.UtcNow,
                LastUsedAt = DateTime.UtcNow
            };

            _dbContext.UserLockers.Add(userLocker);
            await _dbContext.SaveChangesAsync();

            return userLocker;
        }

        public async Task<UserLocker> AddFavoriteLockerByCode(Guid userId, string lockerCode)
        {
            var locker = await _dbContext.Lockers
                .FirstOrDefaultAsync(l => l.LockerCode == lockerCode);

            if (locker == null)
            {
                // Try to get locker from PUDO API
                locker = await _pudoLockerService.GetLockerByCode(lockerCode);
                
                // Save to database if not exists
                if (!await _dbContext.Lockers.AnyAsync(l => l.LockerCode == lockerCode))
                {
                    _dbContext.Lockers.Add(locker);
                    await _dbContext.SaveChangesAsync();
                }
            }

            return await AddFavoriteLocker(userId, locker.Id);
        }

        public async Task<bool> RemoveFavoriteLocker(Guid userId, Guid lockerId)
        {
            var userLocker = await _dbContext.UserLockers
                .FirstOrDefaultAsync(ul => ul.UserId == userId && ul.LockerId == lockerId);

            if (userLocker == null)
            {
                return false;
            }

            if (userLocker.IsDefault || userLocker.IsSellerDropoff)
            {
                // Only remove favorite status, keep the record
                userLocker.IsFavorite = false;
                await _dbContext.SaveChangesAsync();
            }
            else
            {
                // Remove the record completely
                _dbContext.UserLockers.Remove(userLocker);
                await _dbContext.SaveChangesAsync();
            }

            return true;
        }

        public async Task<bool> RemoveFavoriteLockerByCode(Guid userId, string lockerCode)
        {
            var locker = await _dbContext.Lockers
                .FirstOrDefaultAsync(l => l.LockerCode == lockerCode);

            if (locker == null)
            {
                return false;
            }

            return await RemoveFavoriteLocker(userId, locker.Id);
        }

        public async Task<UserLocker> SetDefaultLocker(Guid userId, Guid lockerId)
        {
            // Clear any existing default locker
            var existingDefaults = await _dbContext.UserLockers
                .Where(ul => ul.UserId == userId && ul.IsDefault)
                .ToListAsync();

            foreach (var existing in existingDefaults)
            {
                existing.IsDefault = false;
            }

            // Find or create user locker record
            var userLocker = await _dbContext.UserLockers
                .FirstOrDefaultAsync(ul => ul.UserId == userId && ul.LockerId == lockerId);

            if (userLocker == null)
            {
                var locker = await _dbContext.Lockers.FindAsync(lockerId);
                if (locker == null)
                {
                    throw new ArgumentException($"Locker with ID {lockerId} not found");
                }

                userLocker = new UserLocker
                {
                    UserId = userId,
                    LockerId = lockerId,
                    IsFavorite = true,
                    IsDefault = true,
                    IsSellerDropoff = false,
                    CreatedAt = DateTime.UtcNow,
                    LastUsedAt = DateTime.UtcNow
                };

                _dbContext.UserLockers.Add(userLocker);
            }
            else
            {
                userLocker.IsDefault = true;
                userLocker.IsFavorite = true; // Default lockers are also favorites
                userLocker.LastUsedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();
            return userLocker;
        }

        public async Task<UserLocker> SetDefaultLockerByCode(Guid userId, string lockerCode)
        {
            var locker = await _dbContext.Lockers
                .FirstOrDefaultAsync(l => l.LockerCode == lockerCode);

            if (locker == null)
            {
                // Try to get locker from PUDO API
                locker = await _pudoLockerService.GetLockerByCode(lockerCode);
                
                // Save to database if not exists
                if (!await _dbContext.Lockers.AnyAsync(l => l.LockerCode == lockerCode))
                {
                    _dbContext.Lockers.Add(locker);
                    await _dbContext.SaveChangesAsync();
                }
            }

            return await SetDefaultLocker(userId, locker.Id);
        }

        public async Task<UserLocker> SetSellerDropoffLocker(Guid sellerId, Guid lockerId)
        {
            // Find or create user locker record
            var userLocker = await _dbContext.UserLockers
                .FirstOrDefaultAsync(ul => ul.UserId == sellerId && ul.LockerId == lockerId);

            if (userLocker == null)
            {
                var locker = await _dbContext.Lockers.FindAsync(lockerId);
                if (locker == null)
                {
                    throw new ArgumentException($"Locker with ID {lockerId} not found");
                }

                userLocker = new UserLocker
                {
                    UserId = sellerId,
                    LockerId = lockerId,
                    IsFavorite = true,
                    IsDefault = false,
                    IsSellerDropoff = true,
                    CreatedAt = DateTime.UtcNow,
                    LastUsedAt = DateTime.UtcNow
                };

                _dbContext.UserLockers.Add(userLocker);
            }
            else
            {
                userLocker.IsSellerDropoff = true;
                userLocker.IsFavorite = true; // Seller dropoff lockers are also favorites
                userLocker.LastUsedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();
            return userLocker;
        }

        public async Task<UserLocker> SetSellerDropoffLockerByCode(Guid sellerId, string lockerCode)
        {
            var locker = await _dbContext.Lockers
                .FirstOrDefaultAsync(l => l.LockerCode == lockerCode);

            if (locker == null)
            {
                // Try to get locker from PUDO API
                locker = await _pudoLockerService.GetLockerByCode(lockerCode);
                
                // Save to database if not exists
                if (!await _dbContext.Lockers.AnyAsync(l => l.LockerCode == lockerCode))
                {
                    _dbContext.Lockers.Add(locker);
                    await _dbContext.SaveChangesAsync();
                }
            }

            return await SetSellerDropoffLocker(sellerId, locker.Id);
        }
    }
}
