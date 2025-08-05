using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using GateSale.Core.DTOs;
using GateSale.Core.Entities;
using GateSale.Core.Enums;
using GateSale.Core.Exceptions;
using GateSale.Core.Interfaces;
using GateSale.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace GateSale.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ICognitoService _cognitoService;
        private readonly IEmailService _emailService;
        private readonly IDomainValidationService _domainValidationService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;
        private readonly GateSaleDbContext _dbContext;
        
        // Track email resend attempts to prevent abuse
        private static readonly Dictionary<string, (int Count, DateTime LastAttempt)> _resendAttempts = 
            new Dictionary<string, (int Count, DateTime LastAttempt)>();
        private const int MaxResendAttempts = 3;
        private static readonly TimeSpan ResendCooldown = TimeSpan.FromHours(1);

        public AuthController(
            ICognitoService cognitoService,
            IEmailService emailService,
            IDomainValidationService domainValidationService,
            IConfiguration configuration,
            ILogger<AuthController> logger,
            GateSaleDbContext dbContext)
        {
            _cognitoService = cognitoService;
            _emailService = emailService;
            _domainValidationService = domainValidationService;
            _configuration = configuration;
            _logger = logger;
            _dbContext = dbContext;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDto model)
        {
            // Validate input
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Extract domain from email
                var email = model.Email.ToLower().Trim();
                var emailParts = email.Split('@');
                
                if (emailParts.Length != 2)
                {
                    return BadRequest(new { message = "Invalid email format" });
                }
                
                var domain = emailParts[1];
                
                // Check if domain is whitelisted
                var isDomainWhitelisted = await _domainValidationService.IsDomainWhitelistedAsync(domain);
                
                if (!isDomainWhitelisted)
                {
                    return BadRequest(new { 
                        message = "Your school is not yet supported", 
                        domain = domain,
                        status = "UnsupportedSchool" 
                    });
                }

                // Check if user already exists in our database
                var existingUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (existingUser != null)
                {
                    return BadRequest(new { message = "User already exists" });
                }

                AuthResponseDto cognitoResponse;
                
                try 
                {
                    // Register user with Cognito - this will handle email verification automatically
                    cognitoResponse = await _cognitoService.RegisterUserAsync(model);
                }
                catch (ApplicationException ex) when (ex.Message == "An account with this email already exists.")
                {
                    // User exists in Cognito but not in our database - create local record
                    _logger.LogInformation("User exists in Cognito but not in local database. Creating local record.");
                    
                    // Try to login to get the Cognito user ID
                    try 
                    {
                        var loginDto = new LoginDto { Email = model.Email, Password = model.Password };
                        cognitoResponse = await _cognitoService.LoginAsync(loginDto);
                    }
                    catch (Exception loginEx)
                    {
                        _logger.LogError(loginEx, "Error logging in user that exists in Cognito but not local database");
                        return BadRequest(new { message = "Account exists but unable to authenticate. Try resetting your password." });
                    }
                }
                
                // Create local user record in our database
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Username = email,
                    Email = email,
                    FullName = model.FullName,
                    School = model.School,
                    Grade = model.Grade,
                    DateOfBirth = DateTime.SpecifyKind(model.DateOfBirth, DateTimeKind.Utc),
                    IsEmailVerified = false,
                    Status = UserStatus.PendingEmailVerification,
                    IsMinor = IsMinor(model.DateOfBirth),
                    CognitoUserId = cognitoResponse.UserId ?? string.Empty
                };

                _dbContext.Users.Add(user);
                await _dbContext.SaveChangesAsync();

                return Ok(new { 
                    message = "Registration successful. Please check your email to verify your account.",
                    status = "PendingEmailVerification"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { message = "An error occurred during registration" });
            }
        }

        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Confirm signup with Cognito
                var result = await _cognitoService.ConfirmSignUpAsync(model.Email, model.Code);
                if (!result)
                {
                    return BadRequest(new { message = "Email verification failed" });
                }

                // Update our local user record
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                user.IsEmailVerified = true;
                user.Status = user.IsMinor ? UserStatus.PendingParentalConsent : UserStatus.Active;
                
                await _dbContext.SaveChangesAsync();

                // If the user is not a minor, they are good to go
                if (!user.IsMinor)
                {
                    return Ok(new { 
                        message = "Email verification successful. You can now log in.", 
                        status = "Active" 
                    });
                }

                // If the user is a minor, they need parental consent
                return Ok(new { 
                    message = "Email verification successful. As you are under 18, you need parental consent to fully access the platform.",
                    status = "PendingParentalConsent",
                    requiresParentalConsent = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during email verification");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { message = "An error occurred during email verification" });
            }
        }
        
        [HttpPost("resend-verification")]
        public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var email = model.Email.ToLower().Trim();
                
                // Check if user exists
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    // For security reasons, don't reveal that the user doesn't exist
                    return Ok(new { message = "If your account exists, a verification email has been sent." });
                }

                // Check if email is already verified
                if (user.IsEmailVerified)
                {
                    // Don't reveal this is the reason, just tell them to try logging in
                    return Ok(new { 
                        message = "Your email is already verified. Please try logging in.", 
                        status = "AlreadyVerified" 
                    });
                }

                // Check if user has exceeded the resend attempts limit
                if (_resendAttempts.TryGetValue(email, out var attempts))
                {
                    if (attempts.Count >= MaxResendAttempts && 
                        DateTime.UtcNow - attempts.LastAttempt < ResendCooldown)
                    {
                        var retryAfterTime = attempts.LastAttempt + ResendCooldown;
                        var minutesLeft = Math.Max(1, (int)(retryAfterTime - DateTime.UtcNow).TotalMinutes);
                        
                        return BadRequest(new { 
                            message = $"Too many attempts. Please try again after {minutesLeft} minutes.", 
                            retryAfter = retryAfterTime,
                            status = "TooManyAttempts"
                        });
                    }
                }

                // Resend verification code via Cognito
                var result = await _cognitoService.ResendConfirmationCodeAsync(email);
                if (!result)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, 
                        new { message = "Failed to resend verification code" });
                }

                // Update resend attempts tracking
                if (_resendAttempts.ContainsKey(email))
                {
                    var currentCount = _resendAttempts[email].Count;
                    _resendAttempts[email] = (currentCount + 1, DateTime.UtcNow);
                }
                else
                {
                    _resendAttempts[email] = (1, DateTime.UtcNow);
                }

                return Ok(new { 
                    message = "Verification email sent. Please check your inbox and spam folder.", 
                    status = "EmailSent"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resending verification email");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { message = "An error occurred while sending the verification email" });
            }
        }
        
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Authenticate with Cognito
                var authResult = await _cognitoService.LoginAsync(model);
                
                // Get the user from our database
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
                if (user == null)
                {
                    // This should not happen if the user was properly created during registration
                    return BadRequest(new { message = "User not found in database. Please contact support." });
                }

                // Update last login time
                user.LastLoginAt = DateTime.UtcNow;
                
                // Update CognitoUserId if it's empty
                if (string.IsNullOrEmpty(user.CognitoUserId) && !string.IsNullOrEmpty(authResult.UserId))
                {
                    user.CognitoUserId = authResult.UserId;
                }

                // Check if email is verified in Cognito token but not in our database
                bool isEmailVerifiedInToken = false;
                
                // Extract token and check email_verified claim
                if (!string.IsNullOrEmpty(authResult.Token))
                {
                    try
                    {
                        var tokenParts = authResult.Token.Split('.');
                        if (tokenParts.Length >= 2)
                        {
                            // Decode the payload (second part of the token)
                            var payloadBase64 = tokenParts[1];
                            
                            // Ensure proper padding
                            while (payloadBase64.Length % 4 != 0)
                            {
                                payloadBase64 += "=";
                            }
                            
                            // Convert to proper base64url format for decoding
                            payloadBase64 = payloadBase64.Replace('-', '+').Replace('_', '/');
                            
                            // Decode payload
                            var payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(payloadBase64));
                            
                            // Parse JSON
                            using var jsonDoc = JsonDocument.Parse(payloadJson);
                            
                            // Check if email_verified claim exists and is true
                            isEmailVerifiedInToken = jsonDoc.RootElement.TryGetProperty("email_verified", out var emailVerifiedElement) && 
                                                  emailVerifiedElement.ValueKind == JsonValueKind.True;
                            
                            _logger.LogInformation("Email verified in token: {IsVerified} for user {Email}", isEmailVerifiedInToken, model.Email);
                        }
                    }
                    catch (Exception tokenEx)
                    {
                        _logger.LogError(tokenEx, "Failed to extract verification status from token for user {Email}", model.Email);
                    }
                }
                
                // Update user status if email is verified in token but not in database
                if (isEmailVerifiedInToken && !user.IsEmailVerified)
                {
                    _logger.LogInformation("Automatically updating email verification status for {Email} based on token", model.Email);
                    user.IsEmailVerified = true;
                    user.Status = user.IsMinor ? UserStatus.PendingParentalConsent : UserStatus.Active;
                }
                
                await _dbContext.SaveChangesAsync();

                // Update the response with local database information
                authResult.User.Id = user.Id;
                authResult.User.Status = user.Status.ToString();
                authResult.User.IsEmailVerified = user.IsEmailVerified;
                authResult.User.ParentalConsentGiven = user.ParentalConsentGiven;
                authResult.User.ProfileImageUrl = user.ProfileImageUrl;
                authResult.User.IsProfileComplete = user.IsProfileComplete;

                return Ok(authResult);
            }
            catch (UserNotVerifiedException ex)
            {
                _logger.LogWarning("User {Email} attempted to login but account is not verified", ex.Email);
                
                // Try to automatically resend a verification code for better UX
                try
                {
                    await _cognitoService.ResendConfirmationCodeAsync(model.Email);
                    
                    return BadRequest(new { 
                        message = "Your email address has not been verified. We've sent a new verification code to your email. Please check your inbox and spam folder.",
                        status = "PendingEmailVerification",
                        errorCode = "EMAIL_NOT_VERIFIED",
                        verificationCodeSent = true
                    });
                }
                catch
                {
                    // If resending fails, just return the standard message
                    return BadRequest(new { 
                        message = "Your email address has not been verified. Please check your email for a verification code or request a new one.",
                        status = "PendingEmailVerification",
                        errorCode = "EMAIL_NOT_VERIFIED",
                        verificationCodeSent = false
                    });
                }
            }
            catch (ApplicationException ex) when (ex.Message.Contains("User is not confirmed"))
            {
                _logger.LogWarning(ex, "User {Email} attempted to login but account is not confirmed", model.Email);
                
                // Try to automatically resend a verification code for better UX
                try
                {
                    await _cognitoService.ResendConfirmationCodeAsync(model.Email);
                    
                    return BadRequest(new { 
                        message = "Your email address has not been verified. We've sent a new verification code to your email. Please check your inbox and spam folder.",
                        status = "PendingEmailVerification",
                        errorCode = "EMAIL_NOT_VERIFIED",
                        verificationCodeSent = true
                    });
                }
                catch
                {
                    // If resending fails, just return the standard message
                    return BadRequest(new { 
                        message = "Your email address has not been verified. Please check your email for a verification code or request a new one.",
                        status = "PendingEmailVerification",
                        errorCode = "EMAIL_NOT_VERIFIED",
                        verificationCodeSent = false
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { message = "An error occurred during login" });
            }
        }
        
        [HttpPost("submit-parent")]
        public async Task<IActionResult> SubmitParent([FromBody] ParentDetailsDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Get the current user
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == model.StudentEmail);
                if (user == null)
                {
                    return NotFound(new { message = "User not found" });
                }

                // Verify user is in correct status
                if (user.Status != UserStatus.PendingParentalConsent)
                {
                    return BadRequest(new { 
                        message = "Parental consent is not required or has already been completed",
                        status = user.Status.ToString()
                    });
                }

                // Update parent information
                user.ParentEmail = model.ParentEmail;
                await _dbContext.SaveChangesAsync();

                // Generate a unique token for parent consent
                var consentToken = Guid.NewGuid().ToString();

                // Create or update parental consent record
                var existingConsent = await _dbContext.ParentalConsents.FirstOrDefaultAsync(pc => pc.UserId == user.Id);
                
                if (existingConsent != null)
                {
                    existingConsent.ParentEmail = model.ParentEmail;
                    existingConsent.ConsentToken = consentToken;
                    existingConsent.RequestedAt = DateTime.UtcNow;
                    existingConsent.ExpiresAt = DateTime.UtcNow.AddDays(7);
                    _dbContext.ParentalConsents.Update(existingConsent);
                }
                else
                {
                    var parentalConsent = new ParentalConsent
                    {
                        UserId = user.Id,
                        ParentEmail = model.ParentEmail,
                        ConsentToken = consentToken,
                        IsConsentGiven = false,
                        RequestedAt = DateTime.UtcNow,
                        ExpiresAt = DateTime.UtcNow.AddDays(7)
                    };
                    
                    _dbContext.ParentalConsents.Add(parentalConsent);
                }
                
                await _dbContext.SaveChangesAsync();

                // Send email to parent - wrapped in try/catch to continue even if email fails
                var callbackUrl = $"{_configuration["AppSettings:WebsiteUrl"]}/auth/parent-consent";
                var emailSent = true;
                
                try
                {
                    await _emailService.SendParentalConsentEmailAsync(
                        model.ParentEmail, 
                        model.ParentName, 
                        user.FullName, 
                        consentToken, 
                        callbackUrl);
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, "Failed to send parental consent email, but request was processed");
                    emailSent = false;
                }

                // For testing purposes, include the consent token in the response
                var response = new { 
                    message = emailSent 
                        ? "Parental consent request sent successfully" 
                        : "Parental consent request processed but email failed to send",
                    status = "PendingParentalConsent",
                    consentUrl = $"{callbackUrl}?token={consentToken}"
                };
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending parental consent request");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { message = "An error occurred while processing your request" });
            }
        }
        
        [HttpGet("parent-consent")]
        public async Task<IActionResult> ParentConsent([FromQuery] string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return BadRequest(new { message = "Token is required" });
            }

            try
            {
                // Find the parental consent record with this token
                var parentalConsent = await _dbContext.ParentalConsents
                    .Include(pc => pc.User)
                    .FirstOrDefaultAsync(pc => pc.ConsentToken == token && !pc.IsConsentGiven);
                
                if (parentalConsent == null)
                {
                    return NotFound(new { message = "Invalid or expired consent request" });
                }

                if (DateTime.UtcNow > parentalConsent.ExpiresAt)
                {
                    return BadRequest(new { message = "Consent request has expired. Please request a new one." });
                }

                // Update the parental consent
                parentalConsent.IsConsentGiven = true;
                parentalConsent.ConsentGivenAt = DateTime.UtcNow;
                
                // Update the user status
                var user = parentalConsent.User;
                user.ParentalConsentGiven = true;
                user.ParentalConsentDate = DateTime.UtcNow;
                user.Status = UserStatus.Active;
                
                await _dbContext.SaveChangesAsync();

                return Ok(new { 
                    message = "Thank you! Parental consent has been confirmed.",
                    studentName = user.FullName,
                    studentEmail = user.Email
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming parental consent");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { message = "An error occurred while processing your request" });
            }
        }

        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmailRedirect([FromQuery] string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                return BadRequest(new { message = "Email is required" });
            }

            try
            {
                // Find the user in our database
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    _logger.LogWarning("User not found in database during email verification redirect: {Email}", email);
                    return Redirect($"{_configuration["AppSettings:WebsiteUrl"]}/auth/verification-error");
                }

                try
                {
                    // Check if the user's email is already verified in Cognito
                    bool isVerifiedInCognito = await _cognitoService.IsEmailVerifiedAsync(email);
                    
                    if (isVerifiedInCognito)
                    {
                        // Update our database to match Cognito's verification status if needed
                        if (!user.IsEmailVerified)
                        {
                            user.IsEmailVerified = true;
                            user.Status = user.IsMinor ? UserStatus.PendingParentalConsent : UserStatus.Active;
                            await _dbContext.SaveChangesAsync();
                            
                            _logger.LogInformation("Updated user email verification status for {Email}", email);
                        }
                        
                        // Redirect to success page
                        return Redirect($"{_configuration["AppSettings:WebsiteUrl"]}/auth/email-verified?status={user.Status}");
                    }
                    else
                    {
                        // Not verified in Cognito yet, redirect to verification pending page
                        return Redirect($"{_configuration["AppSettings:WebsiteUrl"]}/auth/verification-pending");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking Cognito verification status for {Email}", email);
                    // Still update our database if we can't check Cognito (might be permissions issue)
                    if (!user.IsEmailVerified)
                    {
                        // Assume verification was successful if we got here through email redirect
                        user.IsEmailVerified = true;
                        user.Status = user.IsMinor ? UserStatus.PendingParentalConsent : UserStatus.Active;
                        await _dbContext.SaveChangesAsync();
                        _logger.LogInformation("Updated user email verification status despite Cognito error for {Email}", email);
                    }
                    
                    return Redirect($"{_configuration["AppSettings:WebsiteUrl"]}/auth/email-verified?status={user.Status}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing email verification redirect for {Email}", email);
                return Redirect($"{_configuration["AppSettings:WebsiteUrl"]}/auth/verification-error");
            }
        }
        
        [HttpPost("sync-verification-status")]
        public async Task<IActionResult> SyncVerificationStatus([FromBody] string email, [FromHeader(Name = "Authorization")] string authorization)
        {
            if (string.IsNullOrEmpty(email))
            {
                return BadRequest(new { message = "Email is required" });
            }
            
            try
            {
                // Get the JWT token from the Authorization header
                if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer "))
                {
                    return BadRequest(new { message = "Authorization header with Bearer token is required" });
                }
                
                var token = authorization.Substring("Bearer ".Length).Trim();
                bool isEmailVerified = false;
                
                try
                {
                    // Decode the JWT token (just basic string manipulation, not validation)
                    var tokenParts = token.Split('.');
                    if (tokenParts.Length >= 2)
                    {
                        // Decode the payload (second part of the token)
                        var payloadBase64 = tokenParts[1];
                        
                        // Ensure proper padding
                        while (payloadBase64.Length % 4 != 0)
                        {
                            payloadBase64 += "=";
                        }
                        
                        // Convert to proper base64url format for decoding
                        payloadBase64 = payloadBase64.Replace('-', '+').Replace('_', '/');
                        
                        // Decode payload
                        var payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(payloadBase64));
                        
                        // Parse JSON
                        using var jsonDoc = JsonDocument.Parse(payloadJson);
                        
                        // Check if email_verified claim exists and is true
                        isEmailVerified = jsonDoc.RootElement.TryGetProperty("email_verified", out var emailVerifiedElement) && 
                                        emailVerifiedElement.ValueKind == JsonValueKind.True;
                    }
                }
                catch (Exception tokenEx)
                {
                    _logger.LogError(tokenEx, "Failed to extract verification status from token");
                    return BadRequest(new { message = "Invalid token format" });
                }
                
                if (isEmailVerified)
                {
                    // Update local database
                    var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
                    if (user != null && !user.IsEmailVerified)
                    {
                        user.IsEmailVerified = true;
                        user.Status = user.IsMinor ? UserStatus.PendingParentalConsent : UserStatus.Active;
                        await _dbContext.SaveChangesAsync();
                        
                        _logger.LogInformation("Manually synced verification status from JWT token for {Email}", email);
                        
                        return Ok(new { 
                            message = "Verification status synced successfully", 
                            status = user.Status.ToString(),
                            isEmailVerified = user.IsEmailVerified,
                            userId = user.Id
                        });
                    }
                    else if (user != null && user.IsEmailVerified)
                    {
                        return Ok(new { 
                            message = "Email is already verified", 
                            status = user.Status.ToString(),
                            isEmailVerified = user.IsEmailVerified,
                            userId = user.Id
                        });
                    }
                }
                
                return BadRequest(new { message = "Could not sync verification status. Email may not be verified." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing verification status for {Email}", email);
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { message = "An error occurred while syncing verification status" });
            }
        }
        
        private bool IsMinor(DateTime dateOfBirth)
        {
            var today = DateTime.UtcNow.Date;
            var age = today.Year - dateOfBirth.Year;
            
            // Adjust age if the birthday hasn't occurred yet this year
            if (dateOfBirth.Date > today.AddYears(-age))
            {
                age--;
            }
            
            return age < 30;
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var email = model.Email.ToLower().Trim();
                
                // Check if user exists in our database
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    // For security reasons, don't reveal that the user doesn't exist
                    return Ok(new { message = "If your account exists, a password reset code has been sent to your email." });
                }

                // Request password reset from Cognito
                await _cognitoService.RequestForgotPasswordAsync(email);
                
                return Ok(new { 
                    message = "A password reset code has been sent to your email. The code is 6 digits.",
                    email = email,
                    status = "CodeSent" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting password reset");
                // For security, still return success to avoid revealing if the account exists
                return Ok(new { message = "If your account exists, a password reset code has been sent to your email." });
            }
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var email = model.Email.ToLower().Trim();
                
                // Confirm password reset with Cognito
                var result = await _cognitoService.ConfirmForgotPasswordAsync(email, model.Code, model.NewPassword);
                if (!result)
                {
                    return BadRequest(new { message = "Password reset failed. Please try again." });
                }
                
                return Ok(new { 
                    message = "Your password has been reset successfully. You can now log in with your new password.",
                    status = "PasswordReset" 
                });
            }
            catch (ApplicationException ex) when (ex.Message == "Invalid verification code.")
            {
                _logger.LogWarning("Invalid verification code for password reset: {Email}", model.Email);
                return BadRequest(new { message = "Invalid verification code.", errorCode = "INVALID_CODE" });
            }
            catch (ApplicationException ex) when (ex.Message == "Password does not meet the requirements.")
            {
                _logger.LogWarning("Password does not meet requirements: {Email}", model.Email);
                return BadRequest(new { 
                    message = "Password does not meet the requirements. Please use at least 8 characters, including uppercase, lowercase, and numbers.",
                    errorCode = "INVALID_PASSWORD"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while resetting your password." });
            }
        }

        [HttpPost("verify-reset-code")]
        public async Task<IActionResult> VerifyResetCode([FromBody] VerifyResetCodeDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // To verify the code without changing the password yet, we'll use a temporary password
                // and catch the expected error
                var tempPassword = Guid.NewGuid().ToString();
                
                try
                {
                    await _cognitoService.ConfirmForgotPasswordAsync(model.Email, model.Code, tempPassword);
                    
                    // If we get here, the code is valid but the password might be too simple
                    // This is unlikely given our random GUID password, but handle it anyway
                    return Ok(new { 
                        message = "Code verified successfully. You can now reset your password.",
                        isValid = true 
                    });
                }
                catch (ApplicationException ex) when (ex.Message == "Invalid verification code.")
                {
                    // Code is invalid
                    return Ok(new { 
                        message = "Invalid verification code. Please check and try again.",
                        isValid = false 
                    });
                }
                catch (ApplicationException ex) when (ex.Message == "Password does not meet the requirements.")
                {
                    // Code is valid, but password doesn't meet requirements (which is fine for now)
                    return Ok(new { 
                        message = "Code verified successfully. You can now reset your password.",
                        isValid = true 
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying reset code");
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { message = "An error occurred while verifying your code." });
            }
        }
    }
    
    public class ParentDetailsDto
    {
        [Required]
        public required string ParentName { get; set; }
        
        [Required]
        [EmailAddress]
        public required string ParentEmail { get; set; }
        
        [Required]
        [EmailAddress]
        public required string StudentEmail { get; set; }
    }

    public class ResendVerificationDto
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }
    }
} 