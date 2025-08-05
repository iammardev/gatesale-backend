using GateSale.Core.Entities;

namespace GateSale.Core.Interfaces
{
    public interface IDomainValidationService
    {
        Task<bool> IsDomainWhitelistedAsync(string emailDomain);
        Task<WhitelistedDomain?> GetWhitelistedDomainAsync(string emailDomain);
        Task<IEnumerable<WhitelistedDomain>> GetAllWhitelistedDomainsAsync();
    }
} 