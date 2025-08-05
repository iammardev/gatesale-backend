using GateSale.Core.DTOs;
using GateSale.Core.Entities;
using Microsoft.AspNetCore.Http;

namespace GateSale.Core.Interfaces
{
    public interface IProductService
    {
        /// <summary>
        /// Creates a new product
        /// </summary>
        Task<Guid> CreateProduct(CreateProductDto productDto, Guid sellerId);

        /// <summary>
        /// Gets products with optional filtering
        /// </summary>
        Task<ProductListDto> GetProducts(ProductFilterDto filter);

        /// <summary>
        /// Gets products for a specific seller with optional filtering
        /// </summary>
        Task<ProductListDto> GetProductsBySeller(Guid sellerId, ProductFilterDto filter);

        /// <summary>
        /// Gets a specific product by ID
        /// </summary>
        Task<ProductDto?> GetProductById(Guid productId);

        /// <summary>
        /// Updates a product
        /// </summary>
        /// <returns>True if successful, false if product not found or user not authorized</returns>
        Task<bool> UpdateProduct(Guid productId, UpdateProductDto productDto, Guid userId);

        /// <summary>
        /// Deletes a product (sets status to Deleted)
        /// </summary>
        /// <returns>True if successful, false if product not found or user not authorized</returns>
        Task<bool> DeleteProduct(Guid productId, Guid userId);

        /// <summary>
        /// Adds images to an existing product
        /// </summary>
        /// <returns>True if successful, false if product not found or user not authorized</returns>
        Task<bool> AddProductImages(Guid productId, List<IFormFile> images, Guid userId);

        /// <summary>
        /// Deletes a product image
        /// </summary>
        /// <returns>True if successful, false if image not found or user not authorized</returns>
        Task<bool> DeleteProductImage(Guid imageId, Guid userId);
    }
} 