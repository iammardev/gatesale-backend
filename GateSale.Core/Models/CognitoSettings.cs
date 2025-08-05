namespace GateSale.Core.Models
{
    public class CognitoSettings
    {
        public string Region { get; set; } = string.Empty;
        public string PoolId { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string Authority { get; set; } = string.Empty;
    }
} 