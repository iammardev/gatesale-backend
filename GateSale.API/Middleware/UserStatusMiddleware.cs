using GateSale.Core.Entities;
using GateSale.Core.Enums;
using GateSale.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GateSale.API.Middleware
{
    public class UserStatusMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<UserStatusMiddleware> _logger;

        // Read-only endpoints that should be accessible even if parental consent is pending
        private readonly string[] _readOnlyEndpoints = new[]
        {
            "/api/products", // product browsing
            "/api/categories",
            "/api/search"
        };

        // Endpoints that are accessible without any verification (public routes)
        private readonly string[] _publicEndpoints = new[]
        {
            "/",
            "/api/auth/login",
            "/api/auth/register",
            "/api/auth/verify-email",
            "/api/auth/resend-verification",
            "/api/auth/parent-consent",
            "/swagger"
        };

        public UserStatusMiddleware(RequestDelegate next, ILogger<UserStatusMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, GateSaleDbContext dbContext)
        {
            var path = context.Request.Path.Value?.ToLowerInvariant();
            
            // Log authentication details for debugging
            _logger.LogInformation(
                "Request Auth Details - Path: {Path}, HasUser: {HasUser}, IsAuthenticated: {IsAuthenticated}, AuthType: {AuthType}",
                context.Request.Path,
                context.User?.Identity != null,
                context.User?.Identity?.IsAuthenticated ?? false,
                context.User?.Identity?.AuthenticationType ?? "None"
            );

            if (context.Request.Headers.Authorization.Count > 0)
            {
                var authHeader = context.Request.Headers.Authorization.ToString();
                _logger.LogInformation("Auth header present for {Path}: starts with {AuthStart}...", 
                    path,
                    authHeader.Length > 15 ? authHeader.Substring(0, 15) + "..." : authHeader);
            }
            else
            {
                _logger.LogWarning("No Authorization header present in request to {Path}", path);
            }
            
            // Skip check for public endpoints or if path is null
            if (path == null || IsPublicEndpoint(path))
            {
                await _next(context);
                return;
            }

            // Skip check if the user is not authenticated
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                await _next(context);
                return;
            }

            // Extract the user's email from claims
            var userEmail = context.User.FindFirst(ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(userEmail))
            {
                await _next(context);
                return;
            }

            try
            {
                // Find the user by email (Cognito uses email as the username)
                var user = await dbContext.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Email == userEmail);
                    
                if (user == null)
                {
                    await _next(context);
                    return;
                }

                switch (user.Status)
                {
                    case UserStatus.PendingEmailVerification:
                        _logger.LogInformation($"User {userEmail} attempted to access {path} but email is not verified");
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(new 
                        { 
                            message = "Email verification required", 
                            status = "PendingEmailVerification" 
                        });
                        return;

                    case UserStatus.PendingParentalConsent:
                        // Allow read-only access to certain endpoints
                        if (IsReadOnlyEndpoint(path) && context.Request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
                        {
                            await _next(context);
                            return;
                        }
                        
                        _logger.LogInformation($"User {userEmail} attempted to access {path} but parental consent is pending");
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(new 
                        { 
                            message = "Parental consent required", 
                            status = "PendingParentalConsent" 
                        });
                        return;

                    case UserStatus.ReadOnly:
                        // Allow read-only access to certain endpoints
                        if (IsReadOnlyEndpoint(path) && context.Request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
                        {
                            await _next(context);
                            return;
                        }
                        
                        _logger.LogInformation($"User {userEmail} attempted to access {path} but account is in read-only mode");
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(new 
                        { 
                            message = "Account is in read-only mode", 
                            status = "ReadOnly" 
                        });
                        return;

                    case UserStatus.Suspended:
                        _logger.LogInformation($"Suspended user {userEmail} attempted to access {path}");
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(new 
                        { 
                            message = "Account is suspended", 
                            status = "Suspended" 
                        });
                        return;

                    case UserStatus.Banned:
                        _logger.LogInformation($"Banned user {userEmail} attempted to access {path}");
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(new 
                        { 
                            message = "Account has been banned", 
                            status = "Banned" 
                        });
                        return;

                    case UserStatus.Active:
                    default:
                        // User is active, proceed with the request
                        await _next(context);
                        return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in UserStatusMiddleware while checking user {userEmail}");
                await _next(context);
            }
        }

        private bool IsPublicEndpoint(string path)
        {
            return _publicEndpoints.Any(endpoint => path.StartsWith(endpoint, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsReadOnlyEndpoint(string path)
        {
            return _readOnlyEndpoints.Any(endpoint => path.StartsWith(endpoint, StringComparison.OrdinalIgnoreCase));
        }
    }

    // Extension method used to add the middleware to the HTTP request pipeline
    public static class UserStatusMiddlewareExtensions
    {
        public static IApplicationBuilder UseUserStatusMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<UserStatusMiddleware>();
        }
    }
} 