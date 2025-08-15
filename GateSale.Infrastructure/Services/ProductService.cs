using GateSale.Core.DTOs;
using GateSale.Core.Entities;
using GateSale.Core.Enums;
using GateSale.Core.Interfaces;
using GateSale.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace GateSale.Infrastructure.Services
{
    public class ProductService : IProductService
    {
        private readonly GateSaleDbContext _context;
        private readonly IStorageService _storageService;
        private readonly ILogger<ProductService> _logger;

        public ProductService(
            GateSaleDbContext context,
            IStorageService storageService,
            ILogger<ProductService> logger)
        {
            _context = context;
            _storageService = storageService;
            _logger = logger;
        }
        
        public async Task<ProductListDto> SearchProducts(ProductSearchDto searchDto)
        {
            try
            {
                var query = _context.Products
                    .Include(p => p.Seller)
                    .Include(p => p.Images)
                    .Where(p => p.Status == ProductStatus.Available);

                // Create a dictionary to store relevance scores for each product
                var productScores = new Dictionary<Guid, int>();
                
                // Get all products, with optional search filtering
                IQueryable<Product> filteredQuery = query;
                
                if (!string.IsNullOrWhiteSpace(searchDto.Q))
                {
                    // Apply search query to title, category, and description with relevance scoring
                    var searchTerm = searchDto.Q.ToLower();
                    
                    filteredQuery = query.Where(p => 
                        p.Title.ToLower().Contains(searchTerm) || 
                        p.Category.ToLower().Contains(searchTerm) || 
                        p.Description.ToLower().Contains(searchTerm));
                }
                
                var matchingProducts = await filteredQuery.ToListAsync();
                
                // Calculate relevance scores
                foreach (var product in matchingProducts)
                {
                    int score = 0;
                    
                    // If search term is provided, calculate relevance score
                    if (!string.IsNullOrWhiteSpace(searchDto.Q))
                    {
                        var searchTerm = searchDto.Q.ToLower();
                        
                        // Title match has highest priority (3 points)
                        if (product.Title.ToLower().Contains(searchTerm))
                        {
                            score += 3;
                        }
                        
                        // Category match has medium priority (2 points)
                        if (product.Category.ToLower().Contains(searchTerm))
                        {
                            score += 2;
                        }
                        
                        // Description match has lowest priority (1 point)
                        if (product.Description.ToLower().Contains(searchTerm))
                        {
                            score += 1;
                        }
                    }
                    
                    productScores[product.Id] = score;
                }
                
                // Filter by category if provided
                if (!string.IsNullOrEmpty(searchDto.CategoryId))
                {
                    matchingProducts = matchingProducts
                        .Where(p => p.Category.ToLower() == searchDto.CategoryId.ToLower())
                        .ToList();
                }
                
                // Filter by price range if provided
                if (searchDto.MinPrice.HasValue)
                {
                    matchingProducts = matchingProducts
                        .Where(p => p.Price >= searchDto.MinPrice.Value)
                        .ToList();
                }
                
                if (searchDto.MaxPrice.HasValue)
                {
                    matchingProducts = matchingProducts
                        .Where(p => p.Price <= searchDto.MaxPrice.Value)
                        .ToList();
                }
                
                // Order by relevance score (descending) then by creation date (descending)
                var orderedProducts = matchingProducts
                    .OrderByDescending(p => productScores[p.Id])
                    .ThenByDescending(p => p.CreatedAt)
                    .Skip(searchDto.Offset)
                    .Take(searchDto.Limit)
                    .ToList();
                
                // Map to DTOs
                var productDtos = orderedProducts.Select(p => new ProductDto
                {
                    Id = p.Id,
                    Title = p.Title,
                    Description = p.Description,
                    Price = p.Price,
                    Category = p.Category,
                    Condition = p.Condition.ToString(),
                    Status = p.Status.ToString(),
                    CreatedAt = p.CreatedAt,
                    Keywords = p.Keywords,
                    SellerId = p.SellerId,
                    SellerName = p.Seller.FullName,
                    SellerSchool = p.Seller.School,
                    Images = p.Images.Select(i => new ProductImageDto
                    {
                        Id = i.Id,
                        ImageUrl = i.ImageUrl,
                        Order = i.Order
                    }).OrderBy(i => i.Order).ToList()
                }).ToList();
                
                return new ProductListDto
                {
                    Products = productDtos,
                    TotalCount = matchingProducts.Count,
                    PageNumber = (searchDto.Offset / searchDto.Limit) + 1,
                    PageSize = searchDto.Limit,
                    TotalPages = (int)Math.Ceiling(matchingProducts.Count / (double)searchDto.Limit)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching products with query: {SearchQuery}", searchDto.Q);
                throw;
            }
        }

        public async Task<Guid> CreateProduct(CreateProductDto productDto, Guid sellerId)
        {
            try
            {
                // Create product entity
                var product = new Product
                {
                    Id = Guid.NewGuid(),
                    Title = productDto.Title,
                    Description = productDto.Description,
                    Price = productDto.Price,
                    Category = productDto.Category,
                    Condition = (ProductCondition)productDto.Condition,
                    Keywords = productDto.Keywords,
                    SellerId = sellerId,
                    CreatedAt = DateTime.UtcNow,
                    Status = ProductStatus.Available
                };

                // Add product to database
                await _context.Products.AddAsync(product);

                // Upload images if any
                if (productDto.Images != null && productDto.Images.Count > 0)
                {
                    var productImages = new List<ProductImage>();
                    int order = 0;

                    foreach (var image in productDto.Images)
                    {
                        try 
                        {
                            _logger.LogInformation("Processing image: {FileName}, ContentType: {ContentType}, Length: {Length} bytes", 
                                image.FileName, image.ContentType, image.Length);
                            
                            var fileName = $"{Guid.NewGuid()}_{image.FileName}";
                            
                            // Convert IFormFile to Stream
                            using var stream = image.OpenReadStream();
                            _logger.LogInformation("Uploading image to S3: {FileName}", fileName);
                            
                            var imageUrl = await _storageService.UploadFileAsync(stream, fileName, image.ContentType);
                            _logger.LogInformation("Successfully uploaded image. URL: {ImageUrl}", imageUrl);

                            productImages.Add(new ProductImage
                            {
                                Id = Guid.NewGuid(),
                                ProductId = product.Id,
                                ImageUrl = imageUrl,
                                FileName = fileName,
                                Order = order++
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error uploading image {FileName} for product {ProductId}", 
                                image.FileName, product.Id);
                            // Continue with the next image instead of failing completely
                        }
                    }

                    await _context.ProductImages.AddRangeAsync(productImages);
                }

                await _context.SaveChangesAsync();
                return product.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product for seller {SellerId}", sellerId);
                throw;
            }
        }

        public async Task<ProductListDto> GetProducts(ProductFilterDto filter)
        {
            try
            {
                var query = _context.Products
                    .Include(p => p.Seller)
                    .Include(p => p.Images)
                    .Where(p => p.Status != ProductStatus.Deleted);

                // Apply filters
                if (!string.IsNullOrEmpty(filter.Category))
                {
                    query = query.Where(p => p.Category.ToLower() == filter.Category.ToLower());
                }

                if (filter.MinPrice.HasValue)
                {
                    query = query.Where(p => p.Price >= filter.MinPrice.Value);
                }

                if (filter.MaxPrice.HasValue)
                {
                    query = query.Where(p => p.Price <= filter.MaxPrice.Value);
                }

                if (!string.IsNullOrEmpty(filter.Condition) && Enum.TryParse<ProductCondition>(filter.Condition, true, out var condition))
                {
                    query = query.Where(p => p.Condition == condition);
                }

                if (!string.IsNullOrEmpty(filter.School))
                {
                    query = query.Where(p => p.Seller.School.ToLower() == filter.School.ToLower());
                }

                if (!string.IsNullOrEmpty(filter.SearchTerm))
                {
                    var searchTerm = filter.SearchTerm.ToLower();
                    query = query.Where(p => 
                        p.Title.ToLower().Contains(searchTerm) || 
                        p.Description.ToLower().Contains(searchTerm) ||
                        (p.Keywords != null && p.Keywords.ToLower().Contains(searchTerm)));
                }

                // Apply sorting
                query = ApplySorting(query, filter.SortBy, filter.SortOrder);

                // Get total count
                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize);

                // Apply pagination
                var products = await query
                    .Skip((filter.PageNumber - 1) * filter.PageSize)
                    .Take(filter.PageSize)
                    .Select(p => new ProductDto
                    {
                        Id = p.Id,
                        Title = p.Title,
                        Description = p.Description,
                        Price = p.Price,
                        Category = p.Category,
                        Condition = p.Condition.ToString(),
                        Status = p.Status.ToString(),
                        CreatedAt = p.CreatedAt,
                        Keywords = p.Keywords,
                        SellerId = p.SellerId,
                        SellerName = p.Seller.FullName,
                        SellerSchool = p.Seller.School,
                        Images = p.Images.Select(i => new ProductImageDto
                        {
                            Id = i.Id,
                            ImageUrl = i.ImageUrl,
                            Order = i.Order
                        }).OrderBy(i => i.Order).ToList()
                    })
                    .ToListAsync();

                return new ProductListDto
                {
                    Products = products,
                    TotalCount = totalCount,
                    PageNumber = filter.PageNumber,
                    PageSize = filter.PageSize,
                    TotalPages = totalPages
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving products");
                throw;
            }
        }

        public async Task<ProductListDto> GetProductsBySeller(Guid sellerId, ProductFilterDto filter)
        {
            try
            {
                // Start with seller filter
                filter.SearchTerm ??= string.Empty;

                var query = _context.Products
                    .Include(p => p.Seller)
                    .Include(p => p.Images)
                    .Where(p => p.SellerId == sellerId && p.Status != ProductStatus.Deleted);

                // Apply filters (same as GetProducts)
                if (!string.IsNullOrEmpty(filter.Category))
                {
                    query = query.Where(p => p.Category.ToLower() == filter.Category.ToLower());
                }

                if (filter.MinPrice.HasValue)
                {
                    query = query.Where(p => p.Price >= filter.MinPrice.Value);
                }

                if (filter.MaxPrice.HasValue)
                {
                    query = query.Where(p => p.Price <= filter.MaxPrice.Value);
                }

                if (!string.IsNullOrEmpty(filter.Condition) && Enum.TryParse<ProductCondition>(filter.Condition, true, out var condition))
                {
                    query = query.Where(p => p.Condition == condition);
                }

                if (!string.IsNullOrEmpty(filter.SearchTerm))
                {
                    var searchTerm = filter.SearchTerm.ToLower();
                    query = query.Where(p => 
                        p.Title.ToLower().Contains(searchTerm) || 
                        p.Description.ToLower().Contains(searchTerm) ||
                        (p.Keywords != null && p.Keywords.ToLower().Contains(searchTerm)));
                }

                // Apply sorting
                query = ApplySorting(query, filter.SortBy, filter.SortOrder);

                // Get total count
                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize);

                // Apply pagination
                var products = await query
                    .Skip((filter.PageNumber - 1) * filter.PageSize)
                    .Take(filter.PageSize)
                    .Select(p => new ProductDto
                    {
                        Id = p.Id,
                        Title = p.Title,
                        Description = p.Description,
                        Price = p.Price,
                        Category = p.Category,
                        Condition = p.Condition.ToString(),
                        Status = p.Status.ToString(),
                        CreatedAt = p.CreatedAt,
                        Keywords = p.Keywords,
                        SellerId = p.SellerId,
                        SellerName = p.Seller.FullName,
                        SellerSchool = p.Seller.School,
                        Images = p.Images.Select(i => new ProductImageDto
                        {
                            Id = i.Id,
                            ImageUrl = i.ImageUrl,
                            Order = i.Order
                        }).OrderBy(i => i.Order).ToList()
                    })
                    .ToListAsync();

                return new ProductListDto
                {
                    Products = products,
                    TotalCount = totalCount,
                    PageNumber = filter.PageNumber,
                    PageSize = filter.PageSize,
                    TotalPages = totalPages
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving products for seller {SellerId}", sellerId);
                throw;
            }
        }

        public async Task<ProductDto?> GetProductById(Guid productId)
        {
            try
            {
                var product = await _context.Products
                    .Include(p => p.Seller)
                    .Include(p => p.Images)
                    .FirstOrDefaultAsync(p => p.Id == productId && p.Status != ProductStatus.Deleted);

                if (product == null)
                {
                    return null;
                }

                return new ProductDto
                {
                    Id = product.Id,
                    Title = product.Title,
                    Description = product.Description,
                    Price = product.Price,
                    Category = product.Category,
                    Condition = product.Condition.ToString(),
                    Status = product.Status.ToString(),
                    CreatedAt = product.CreatedAt,
                    Keywords = product.Keywords,
                    SellerId = product.SellerId,
                    SellerName = product.Seller.FullName,
                    SellerSchool = product.Seller.School,
                    Images = product.Images.Select(i => new ProductImageDto
                    {
                        Id = i.Id,
                        ImageUrl = i.ImageUrl,
                        Order = i.Order
                    }).OrderBy(i => i.Order).ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product {ProductId}", productId);
                throw;
            }
        }

        public async Task<bool> UpdateProduct(Guid productId, UpdateProductDto productDto, Guid userId)
        {
            try
            {
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == productId && p.SellerId == userId);

                if (product == null)
                {
                    return false;
                }

                // Update only provided properties
                if (!string.IsNullOrEmpty(productDto.Title))
                {
                    product.Title = productDto.Title;
                }

                if (!string.IsNullOrEmpty(productDto.Description))
                {
                    product.Description = productDto.Description;
                }

                if (productDto.Price.HasValue)
                {
                    product.Price = productDto.Price.Value;
                }

                if (!string.IsNullOrEmpty(productDto.Category))
                {
                    product.Category = productDto.Category;
                }

                if (productDto.Condition.HasValue)
                {
                    product.Condition = (ProductCondition)productDto.Condition.Value;
                }

                if (productDto.Status.HasValue)
                {
                    product.Status = (ProductStatus)productDto.Status.Value;
                }

                if (productDto.Keywords != null)
                {
                    product.Keywords = productDto.Keywords;
                }

                product.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product {ProductId}", productId);
                throw;
            }
        }

        public async Task<bool> DeleteProduct(Guid productId, Guid userId)
        {
            try
            {
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == productId && p.SellerId == userId);

                if (product == null)
                {
                    return false;
                }

                // Soft delete - set status to Deleted
                product.Status = ProductStatus.Deleted;
                product.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product {ProductId}", productId);
                throw;
            }
        }

        public async Task<bool> AddProductImages(Guid productId, List<IFormFile> images, Guid userId)
        {
            try
            {
                var product = await _context.Products
                    .Include(p => p.Images)
                    .FirstOrDefaultAsync(p => p.Id == productId && p.SellerId == userId);

                if (product == null)
                {
                    return false;
                }

                // Check if adding these would exceed max
                if (product.Images.Count + images.Count > 5)
                {
                    throw new InvalidOperationException("Cannot add more images. Maximum of 5 images allowed per product.");
                }

                var productImages = new List<ProductImage>();
                int order = product.Images.Any() ? product.Images.Max(i => i.Order) + 1 : 0;

                foreach (var image in images)
                {
                    var fileName = $"{Guid.NewGuid()}_{image.FileName}";
                    
                    // Convert IFormFile to Stream
                    using var stream = image.OpenReadStream();
                    var imageUrl = await _storageService.UploadFileAsync(stream, fileName, image.ContentType);

                    productImages.Add(new ProductImage
                    {
                        Id = Guid.NewGuid(),
                        ProductId = product.Id,
                        ImageUrl = imageUrl,
                        FileName = fileName,
                        Order = order++
                    });
                }

                await _context.ProductImages.AddRangeAsync(productImages);
                product.UpdatedAt = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding images to product {ProductId}", productId);
                throw;
            }
        }

        public async Task<bool> DeleteProductImage(Guid imageId, Guid userId)
        {
            try
            {
                var image = await _context.ProductImages
                    .Include(i => i.Product)
                    .FirstOrDefaultAsync(i => i.Id == imageId && i.Product.SellerId == userId);

                if (image == null)
                {
                    return false;
                }

                // Delete image from storage service - just pass the URL
                await _storageService.DeleteFileAsync(image.ImageUrl);

                // Delete from database
                _context.ProductImages.Remove(image);

                // Update product's last modified date
                image.Product.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product image {ImageId}", imageId);
                throw;
            }
        }

        // Helper method to apply sorting
        private static IQueryable<Product> ApplySorting(IQueryable<Product> query, string? sortBy, string? sortOrder)
        {
            var isDescending = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);

            query = sortBy?.ToLower() switch
            {
                "price" => isDescending ? query.OrderByDescending(p => p.Price) : query.OrderBy(p => p.Price),
                "title" => isDescending ? query.OrderByDescending(p => p.Title) : query.OrderBy(p => p.Title),
                "createdat" => isDescending ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt),
                _ => isDescending ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt)
            };

            return query;
        }
    }
} 