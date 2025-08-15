using GateSale.Core.Entities;

namespace GateSale.Core.Interfaces
{
    public interface IUserLockerService
    {
        Task<IEnumerable<UserLocker>> GetUserFavoriteLockers(Guid userId);
        Task<UserLocker?> GetUserDefaultLocker(Guid userId);
        Task<IEnumerable<UserLocker>> GetSellerDropoffLockers(Guid sellerId);
        Task<UserLocker> AddFavoriteLocker(Guid userId, Guid lockerId);
        Task<UserLocker> AddFavoriteLockerByCode(Guid userId, string lockerCode);
        Task<bool> RemoveFavoriteLocker(Guid userId, Guid lockerId);
        Task<bool> RemoveFavoriteLockerByCode(Guid userId, string lockerCode);
        Task<UserLocker> SetDefaultLocker(Guid userId, Guid lockerId);
        Task<UserLocker> SetDefaultLockerByCode(Guid userId, string lockerCode);
        Task<UserLocker> SetSellerDropoffLocker(Guid sellerId, Guid lockerId);
        Task<UserLocker> SetSellerDropoffLockerByCode(Guid sellerId, string lockerCode);
    }
}
