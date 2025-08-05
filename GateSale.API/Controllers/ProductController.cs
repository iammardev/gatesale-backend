using GateSale.Core.DTOs;
using GateSale.Core.Entities;
using GateSale.Core.Enums;
using GateSale.Core.Interfaces;
using GateSale.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Claims;

namespace GateSale.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly IStorageService _storageService;
        private readonly ILogger<ProductController> _logger;
        private readonly GateSale.Infrastructure.Data.GateSaleDbContext _context;

        public ProductController(
            IProductService productService,
            IStorageService storageService,
            GateSale.Infrastructure.Data.GateSaleDbContext context,
            ILogger<ProductController> logger)
        {
            _productService = productService;
            _storageService = storageService;
            _context = context;
            _logger = logger;
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateProduct([FromForm] CreateProductDto productDto)
        {
            try
            {
                // Log all claims for debugging
                _logger.LogInformation("Authorization header present: {AuthHeader}", Request.Headers.Authorization.Count > 0);
                _logger.LogInformation("Claims in token: {ClaimsCount}", User.Claims.Count());
                
                foreach (var claim in User.Claims)
                {
                    _logger.LogInformation("Claim: {Type} = {Value}", claim.Type, claim.Value);
                }
                
                // Get user ID from token
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    _logger.LogWarning("Missing user ID in token. Token may be invalid or expired.");
                    return Unauthorized("Invalid or expired token. Please re-authenticate.");
                }

                // Log user ID for debugging
                _logger.LogInformation("Attempting to create product for user ID: {UserId}", userIdClaim);
                
                // Try to parse user ID
                if (!Guid.TryParse(userIdClaim, out Guid userId))
                {
                    _logger.LogWarning("Invalid user ID format in token: {UserIdClaim}", userIdClaim);
                    return BadRequest("Invalid user ID format");
                }

                // Check if user exists in database
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    // Check if user exists by email
                    var email = User.FindFirstValue(ClaimTypes.Email);
                    if (!string.IsNullOrEmpty(email))
                    {
                        user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                        if (user != null)
                        {
                            // Found user by email, use this ID instead
                            userId = user.Id;
                            _logger.LogInformation("Found user by email: {Email}, ID: {UserId}", email, userId);
                        }
                    }
                    
                    if (user == null)
                    {
                        _logger.LogWarning("User not found in database. ID: {UserId}", userId);
                        return Unauthorized("User not found in system. Please complete your profile first.");
                    }
                }

                // Check if email is verified
                if (!user.IsEmailVerified)
                {
                    return BadRequest("Email verification is required before creating products");
                }

                // Check if parental consent is required and given
                if (user.IsMinor && !user.ParentalConsentGiven)
                {
                    return BadRequest("Parental consent is required before creating products");
                }

                // Validate number of images
                if (productDto.Images != null && productDto.Images.Count > 5)
                {
                    return BadRequest("Maximum 5 images are allowed");
                }

                // Use the verified user ID
                var productId = await _productService.CreateProduct(productDto, user.Id);
                return CreatedAtAction(nameof(GetProduct), new { id = productId }, new { Id = productId });
            }
            catch (DbUpdateException dbEx) when (dbEx.InnerException is PostgresException pgEx && pgEx.SqlState == "23503")
            {
                // Handle foreign key violation specifically
                _logger.LogError(dbEx, "Foreign key constraint violation while creating product");
                return BadRequest("Unable to create product: seller information is invalid.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product");
                return StatusCode(500, "An error occurred while creating the product");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAllProducts([FromQuery] ProductFilterDto filter)
        {
            try
            {
                var products = await _productService.GetProducts(filter);
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving products");
                return StatusCode(500, "An error occurred while retrieving products");
            }
        }

        [HttpGet("my")]
        [Authorize]
        public async Task<IActionResult> GetMyProducts([FromQuery] ProductFilterDto filter)
        {
            try
            {
                // Get user ID from token
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    _logger.LogWarning("Missing user ID in token when retrieving products");
                    return Unauthorized("Invalid user token");
                }

                // Log user ID for debugging
                _logger.LogInformation("Retrieving products for user ID: {UserId}", userIdClaim);
                
                Guid userId;
                // Try to parse user ID
                if (!Guid.TryParse(userIdClaim, out userId))
                {
                    _logger.LogWarning("Invalid user ID format in token: {UserIdClaim}", userIdClaim);
                    return BadRequest("Invalid user ID format");
                }

                // Check if user exists in database
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    // Check if user exists by email
                    var email = User.FindFirstValue(ClaimTypes.Email);
                    if (!string.IsNullOrEmpty(email))
                    {
                        user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                        if (user != null)
                        {
                            // Found user by email, use this ID instead
                            userId = user.Id;
                            _logger.LogInformation("Found user by email when retrieving products: {Email}, ID: {UserId}", email, userId);
                        }
                    }
                    
                    if (user == null)
                    {
                        _logger.LogWarning("User not found in database when retrieving products. ID: {UserId}", userId);
                        return Unauthorized("User not found in system. Please complete your profile first.");
                    }
                }

                var products = await _productService.GetProductsBySeller(user.Id, filter);
                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user products");
                return StatusCode(500, "An error occurred while retrieving your products");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetProduct(Guid id)
        {
            try
            {
                var product = await _productService.GetProductById(id);
                if (product == null)
                {
                    return NotFound();
                }

                return Ok(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product {ProductId}", id);
                return StatusCode(500, "An error occurred while retrieving the product");
            }
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] UpdateProductDto productDto)
        {
            try
            {
                // Get user ID from token
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    _logger.LogWarning("Missing user ID in token when updating product");
                    return Unauthorized("Invalid user token");
                }

                // Log IDs for debugging
                _logger.LogInformation("Attempting to update product {ProductId} by user {UserId}", id, userIdClaim);
                
                // Try to parse user ID
                if (!Guid.TryParse(userIdClaim, out Guid userId))
                {
                    _logger.LogWarning("Invalid user ID format in token: {UserIdClaim}", userIdClaim);
                    return BadRequest("Invalid user ID format");
                }

                // First check if product exists
                var product = await _context.Products.FindAsync(id);
                if (product == null)
                {
                    _logger.LogWarning("Product not found: {ProductId}", id);
                    return NotFound($"Product with ID {id} not found");
                }

                // Check if user exists in database
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    // Check if user exists by email
                    var email = User.FindFirstValue(ClaimTypes.Email);
                    if (!string.IsNullOrEmpty(email))
                    {
                        user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                        if (user != null)
                        {
                            // Found user by email, use this ID instead
                            userId = user.Id;
                            _logger.LogInformation("Found user by email when updating product: {Email}, ID: {UserId}", email, userId);
                        }
                    }
                    
                    if (user == null)
                    {
                        _logger.LogWarning("User not found in database when updating product. ID: {UserId}", userId);
                        return Unauthorized("User not found in system");
                    }
                }

                // Check if user owns the product
                if (product.SellerId != user.Id)
                {
                    _logger.LogWarning("User {UserId} attempted to update product {ProductId} owned by {OwnerId}", 
                        user.Id, id, product.SellerId);
                    return Forbid("You don't have permission to update this product");
                }

                var success = await _productService.UpdateProduct(id, productDto, user.Id);
                if (!success)
                {
                    return StatusCode(500, "Failed to update product");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product {ProductId}", id);
                return StatusCode(500, "An error occurred while updating the product");
            }
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteProduct(Guid id)
        {
            try
            {
                // Get user ID from token
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    _logger.LogWarning("Missing user ID in token when deleting product");
                    return Unauthorized("Invalid user token");
                }

                // Log IDs for debugging
                _logger.LogInformation("Attempting to delete product {ProductId} by user {UserId}", id, userIdClaim);
                
                // Try to parse user ID
                if (!Guid.TryParse(userIdClaim, out Guid userId))
                {
                    _logger.LogWarning("Invalid user ID format in token: {UserIdClaim}", userIdClaim);
                    return BadRequest("Invalid user ID format");
                }

                // First check if product exists
                var product = await _context.Products.FindAsync(id);
                if (product == null)
                {
                    _logger.LogWarning("Product not found: {ProductId}", id);
                    return NotFound($"Product with ID {id} not found");
                }

                // Check if user exists in database
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    // Check if user exists by email
                    var email = User.FindFirstValue(ClaimTypes.Email);
                    if (!string.IsNullOrEmpty(email))
                    {
                        user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                        if (user != null)
                        {
                            // Found user by email, use this ID instead
                            userId = user.Id;
                            _logger.LogInformation("Found user by email when deleting product: {Email}, ID: {UserId}", email, userId);
                        }
                    }
                    
                    if (user == null)
                    {
                        _logger.LogWarning("User not found in database when deleting product. ID: {UserId}", userId);
                        return Unauthorized("User not found in system");
                    }
                }

                // Check if user owns the product
                if (product.SellerId != user.Id)
                {
                    _logger.LogWarning("User {UserId} attempted to delete product {ProductId} owned by {OwnerId}", 
                        user.Id, id, product.SellerId);
                    return Forbid("You don't have permission to delete this product");
                }

                var success = await _productService.DeleteProduct(id, user.Id);
                if (!success)
                {
                    return StatusCode(500, "Failed to delete product");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product {ProductId}", id);
                return StatusCode(500, "An error occurred while deleting the product");
            }
        }

        [HttpPost("{id}/images")]
        [Authorize]
        public async Task<IActionResult> AddProductImages(Guid id, [FromForm] List<IFormFile> images)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userId == null)
                {
                    return Unauthorized();
                }

                // Check number of images
                if (images.Count > 5)
                {
                    return BadRequest("Maximum 5 images are allowed");
                }

                var result = await _productService.AddProductImages(id, images, Guid.Parse(userId));
                if (!result)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding images to product {ProductId}", id);
                return StatusCode(500, "An error occurred while adding images to the product");
            }
        }

        [HttpDelete("images/{imageId}")]
        [Authorize]
        public async Task<IActionResult> DeleteProductImage(Guid imageId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userId == null)
                {
                    return Unauthorized();
                }

                var success = await _productService.DeleteProductImage(imageId, Guid.Parse(userId));
                if (!success)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product image {ImageId}", imageId);
                return StatusCode(500, "An error occurred while deleting the product image");
            }
        }
    }
} 