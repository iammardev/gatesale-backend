namespace GateSale.Core.Entities
{
    public class ProductImage
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public Product Product { get; set; } = null!;
        public required string ImageUrl { get; set; }
        public required string FileName { get; set; }
        public int Order { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}