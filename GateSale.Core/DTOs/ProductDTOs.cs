using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace GateSale.Core.DTOs
{
    public class CreateProductDto
    {
        [Required]
        [MaxLength(200)]
        public required string Title { get; set; }
        
        [Required]
        [MaxLength(2000)]
        public required string Description { get; set; }
        
        [Required]
        [Range(0.01, 10000)]
        public decimal Price { get; set; }
        
        [Required]
        public required string Category { get; set; }
        
        [Required]
        public int Condition { get; set; } // ProductCondition enum value
        
        [MaxLength(500)]
        public string? Keywords { get; set; }
        
        public List<IFormFile>? Images { get; set; }
    }
    
    public class UpdateProductDto
    {
        [MaxLength(200)]
        public string? Title { get; set; }
        
        [MaxLength(2000)]
        public string? Description { get; set; }
        
        [Range(0.01, 10000)]
        public decimal? Price { get; set; }
        
        public string? Category { get; set; }
        public int? Condition { get; set; }
        public int? Status { get; set; } // ProductStatus enum value
        
        [MaxLength(500)]
        public string? Keywords { get; set; }
    }
    
    public class ProductDto
    {
        public Guid Id { get; set; }
        public required string Title { get; set; }
        public required string Description { get; set; }
        public decimal Price { get; set; }
        public required string Category { get; set; }
        public string Condition { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? Keywords { get; set; }
        
        // Seller Info
        public Guid SellerId { get; set; }
        public required string SellerName { get; set; }
        public required string SellerSchool { get; set; }
        
        // Images
        public List<ProductImageDto> Images { get; set; } = new();
    }
    
    public class ProductImageDto
    {
        public Guid Id { get; set; }
        public required string ImageUrl { get; set; }
        public int Order { get; set; }
    }
    
    public class ProductListDto
    {
        public List<ProductDto> Products { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
    
    public class ProductFilterDto
    {
        public string? Category { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string? Condition { get; set; }
        public string? School { get; set; }
        public string? SearchTerm { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? SortBy { get; set; } = "CreatedAt";
        public string? SortOrder { get; set; } = "desc";
    }
}