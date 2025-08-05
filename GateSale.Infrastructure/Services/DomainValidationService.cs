using GateSale.Core.Entities;
using GateSale.Core.Interfaces;
using GateSale.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GateSale.Infrastructure.Services
{
    public class DomainValidationService : IDomainValidationService
    {
        private readonly GateSaleDbContext _dbContext;
        private readonly ILogger<DomainValidationService> _logger;

        public DomainValidationService(GateSaleDbContext dbContext, ILogger<DomainValidationService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<bool> IsDomainWhitelistedAsync(string emailDomain)
        {
            if (string.IsNullOrWhiteSpace(emailDomain))
            {
                return false;
            }

            emailDomain = emailDomain.ToLower().Trim();
            
            try
            {
                var domain = await _dbContext.WhitelistedDomains
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Domain == emailDomain && d.IsActive);
                
                return domain != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking if domain {emailDomain} is whitelisted");
                throw;
            }
        }

        public async Task<WhitelistedDomain?> GetWhitelistedDomainAsync(string emailDomain)
        {
            if (string.IsNullOrWhiteSpace(emailDomain))
            {
                return null;
            }

            emailDomain = emailDomain.ToLower().Trim();
            
            try
            {
                return await _dbContext.WhitelistedDomains
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Domain == emailDomain && d.IsActive);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting whitelisted domain {emailDomain}");
                throw;
            }
        }

        public async Task<IEnumerable<WhitelistedDomain>> GetAllWhitelistedDomainsAsync()
        {
            try
            {
                return await _dbContext.WhitelistedDomains
                    .AsNoTracking()
                    .Where(d => d.IsActive)
                    .OrderBy(d => d.Domain)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all whitelisted domains");
                throw;
            }
        }
    }
} 