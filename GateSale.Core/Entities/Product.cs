using GateSale.Core.Enums;

namespace GateSale.Core.Entities
{
    public class Product
    {
        public Guid Id { get; set; }
        public required string Title { get; set; }
        public required string Description { get; set; }
        public decimal Price { get; set; }
        public required string Category { get; set; }
        public ProductCondition Condition { get; set; }
        public ProductStatus Status { get; set; } = ProductStatus.Available;
        public string? Keywords { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        
        // Seller Information
        public Guid SellerId { get; set; }
        public User Seller { get; set; } = null!;
        
        // Media
        public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
        
        // Relations
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}