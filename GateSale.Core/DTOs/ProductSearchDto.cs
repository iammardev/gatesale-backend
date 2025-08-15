using System.ComponentModel.DataAnnotations;

namespace GateSale.Core.DTOs
{
    public class ProductSearchDto
    {
        public string? Q { get; set; }
        
        public string? CategoryId { get; set; }
        
        [Range(0, double.MaxValue, ErrorMessage = "Minimum price must be a non-negative value")]
        public decimal? MinPrice { get; set; }
        
        [Range(0, double.MaxValue, ErrorMessage = "Maximum price must be a non-negative value")]
        public decimal? MaxPrice { get; set; }
        
        [Range(1, 100, ErrorMessage = "Limit must be between 1 and 100")]
        public int Limit { get; set; } = 20;
        
        [Range(0, int.MaxValue, ErrorMessage = "Offset must be a non-negative value")]
        public int Offset { get; set; } = 0;
    }
}
