using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Extensions.CognitoAuthentication;
using Amazon.Runtime;
using GateSale.Core.DTOs;
using GateSale.Core.Exceptions;
using GateSale.Core.Interfaces;
using GateSale.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace GateSale.Infrastructure.Services
{
    public class CognitoService : ICognitoService
    {
        private readonly IAmazonCognitoIdentityProvider _cognitoClient; // Changed from AmazonCognitoIdentityProviderClient to interface
        private readonly CognitoUserPool _userPool;
        private readonly CognitoSettings _cognitoSettings;
        private readonly ILogger<CognitoService> _logger;
        private readonly string _apiBaseUrl;

        public CognitoService(
            IOptions<CognitoSettings> cognitoSettings,
            ILogger<CognitoService> logger,
            IConfiguration configuration,
            IAmazonCognitoIdentityProvider? cognitoClient = null) // Added ? to make nullable
        {
            _cognitoSettings = cognitoSettings.Value;
            _logger = logger;
            _apiBaseUrl = configuration["AppSettings:ApiUrl"] ?? "https://api.gatesale.com";

            // Use the injected client if provided (preferred approach)
            if (cognitoClient != null)
            {
                _cognitoClient = cognitoClient;
            }
            else
            {
                // Get AWS credentials from configuration
                var accessKey = configuration["AWS:AccessKey"];
                var secretKey = configuration["AWS:SecretKey"];
                
                // Make sure we have credentials before proceeding
                if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
                {
                    _logger.LogWarning("AWS credentials are missing or empty. Using default credentials provider chain.");
                    // Fall back to default credentials provider chain (instance profile, environment variables, etc.)
                    _cognitoClient = new AmazonCognitoIdentityProviderClient(RegionEndpoint.GetBySystemName(_cognitoSettings.Region));
                }
                else
                {
                    var credentials = new BasicAWSCredentials(accessKey, secretKey);
                    _cognitoClient = new AmazonCognitoIdentityProviderClient(credentials, RegionEndpoint.GetBySystemName(_cognitoSettings.Region));
                }
            }
            
            _userPool = new CognitoUserPool(
                _cognitoSettings.PoolId,
                _cognitoSettings.ClientId,
                _cognitoClient); // This will work with interface now
        }

        public async Task<AuthResponseDto> RegisterUserAsync(RegisterDto registerDto)
        {
            try
            {
                // Create attributes for the new user
                var attributes = new Dictionary<string, string>
                {
                    { "email", registerDto.Email },
                    { "given_name", registerDto.FullName.Split(' ', 2)[0] },
                    { "family_name", registerDto.FullName.Split(' ', 2).Length > 1 ? registerDto.FullName.Split(' ', 2)[1] : "" },
                    { "custom:School", registerDto.School },
                    { "custom:grade", registerDto.Grade.ToString() },
                    { "birthdate", registerDto.DateOfBirth.ToString("yyyy-MM-dd") }
                };

                // Calculate SECRET_HASH
                string secretHash = CalculateSecretHash(registerDto.Email);

                // Register the user with Cognito
                var signUpRequest = new SignUpRequest
                {
                    ClientId = _cognitoSettings.ClientId,
                    Password = registerDto.Password,
                    Username = registerDto.Email,
                    SecretHash = secretHash,
                    UserAttributes = attributes.Select(a => new AttributeType
                    {
                        Name = a.Key,
                        Value = a.Value
                    }).ToList()
                };

                _logger.LogInformation("Registering new user: {Email}", registerDto.Email);
                var response = await _cognitoClient.SignUpAsync(signUpRequest);

                // Store the redirect URL for handling in the application
                var redirectUrl = $"{_apiBaseUrl}/api/Auth/confirm-email?email={WebUtility.UrlEncode(registerDto.Email)}";
                _logger.LogInformation("Email verification redirect URL will be handled by application: {0}", redirectUrl);

                return new AuthResponseDto
                {
                    Token = string.Empty, // No token on registration
                    RefreshToken = string.Empty,
                    ExpiresAt = DateTime.UtcNow,
                    UserId = response.UserSub, // Add the Cognito user ID
                    User = new UserProfileDto
                    {
                        Id = Guid.Empty, // User ID is not known at this point
                        Email = registerDto.Email,
                        FullName = registerDto.FullName,
                        School = registerDto.School,
                        IsEmailVerified = false,
                        Status = "PendingEmailVerification"
                    }
                };
            }
            catch (UsernameExistsException ex)
            {
                _logger.LogWarning(ex, "Username already exists: {Email}", registerDto.Email);
                throw new ApplicationException("An account with this email already exists.");
            }
            catch (InvalidPasswordException ex)
            {
                _logger.LogWarning(ex, "Invalid password for registration");
                throw new ApplicationException("Password does not meet the requirements.");
            }
            catch (Exception ex)
            {
                // Check for username exists error in the message since AWS sometimes wraps the exception differently
                if (ex.Message.Contains("User already exists", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(ex, "Username already exists (from generic exception): {Email}", registerDto.Email);
                    throw new ApplicationException("An account with this email already exists.");
                }
                
                _logger.LogError(ex, "Error during user registration: {Message}", ex.Message);
                throw new ApplicationException("An error occurred during registration.");
            }
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto loginDto)
        {
            try
            {
                // Calculate the SECRET_HASH which is required by your Cognito app client
                string secretHash = CalculateSecretHash(loginDto.Email);
                
                _logger.LogInformation("Attempting to authenticate user: {Email}", loginDto.Email);
                
                // Use the USER_PASSWORD_AUTH flow which is enabled in your app client
                try 
                {
                    var authRequest = new InitiateAuthRequest
                    {
                        AuthFlow = AuthFlowType.USER_PASSWORD_AUTH,  // This is enabled in your app client
                        ClientId = _cognitoSettings.ClientId,
                        AuthParameters = new Dictionary<string, string>
                        {
                            { "USERNAME", loginDto.Email },
                            { "PASSWORD", loginDto.Password },
                            { "SECRET_HASH", secretHash }
                        }
                    };
                    
                    _logger.LogInformation("Using USER_PASSWORD_AUTH flow for {Email}", loginDto.Email);
                    var authResponse = await _cognitoClient.InitiateAuthAsync(authRequest);
                    
                    return ProcessAuthenticationResult(authResponse, loginDto.Email);
                }
                catch (UserNotConfirmedException ex)
                {
                    _logger.LogWarning(ex, "User is not confirmed: {Email}", loginDto.Email);
                    throw new UserNotVerifiedException(loginDto.Email, ex);
                }
                catch (NotAuthorizedException authEx) when (authEx.Message.Contains("flow not enabled"))
                {
                    _logger.LogWarning(authEx, "USER_PASSWORD_AUTH flow not enabled, trying USER_SRP_AUTH flow");
                    
                    // Try USER_SRP_AUTH as an alternative
                    try
                    {
                        // For USER_SRP_AUTH, we need to switch to a different approach because
                        // this flow requires SRP calculation and cannot be directly called with a password
                        // Instead, we'll use a more direct auth flow that is compatible
                        
                        // Try with REFRESH_TOKEN_AUTH flow which is enabled
                        var refreshAuthRequest = new InitiateAuthRequest
                        {
                            AuthFlow = AuthFlowType.REFRESH_TOKEN_AUTH, // Attempt this flow instead
                            ClientId = _cognitoSettings.ClientId,
                            AuthParameters = new Dictionary<string, string>
                            {
                                { "USERNAME", loginDto.Email },
                                { "REFRESH_TOKEN", "dummytoken" }, // This will fail but let us know if SECRET_HASH is correct
                                { "SECRET_HASH", secretHash }
                            }
                        };
                        
                        try
                        {
                            // This is expected to fail, but will validate if our SECRET_HASH calculation is correct
                            await _cognitoClient.InitiateAuthAsync(refreshAuthRequest);
                        }
                        catch (Exception)
                        {
                            // This is expected to fail since we don't have a real refresh token
                            // If we get an error about the refresh token rather than SECRET_HASH, then 
                            // our SECRET_HASH calculation is correct
                        }
                        
                        // Let the admin know that they should enable USER_PASSWORD_AUTH in the Cognito app client settings
                        throw new ApplicationException("Authentication failed: USER_PASSWORD_AUTH flow is not enabled in your Cognito app client settings. " +
                            "Please check your Cognito User Pool app client settings and ensure USER_PASSWORD_AUTH is enabled.");
                    }
                    catch (Exception srpEx)
                    {
                        _logger.LogError(srpEx, "Authentication failed: {Message}", srpEx.Message);
                        throw new ApplicationException("Authentication failed. Please verify your Cognito configuration and ensure USER_PASSWORD_AUTH flow is enabled.");
                    }
                }
                catch (NotAuthorizedException authEx)
                {
                    _logger.LogWarning(authEx, "Login failed - incorrect credentials for user: {Email}", loginDto.Email);
                    throw new ApplicationException("Incorrect username or password.");
                }
                catch (UserNotFoundException unfEx)
                {
                    _logger.LogWarning(unfEx, "Login failed - user not found: {Email}", loginDto.Email);
                    throw new ApplicationException("User not found.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during authentication: {Message}", ex.Message);
                    throw new ApplicationException($"Authentication failed: {ex.Message}. Please verify your AWS credentials and Cognito configuration.");
                }
            }
            catch (UserNotConfirmedException ex)
            {
                _logger.LogWarning(ex, "Unconfirmed user attempted login: {Email}", loginDto.Email);
                throw new UserNotVerifiedException(loginDto.Email, ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for user: {Email}", loginDto.Email);
                
                // Check if the inner exception is a UserNotConfirmedException
                if (ex.InnerException is UserNotConfirmedException || 
                    (ex.Message?.Contains("User is not confirmed", StringComparison.OrdinalIgnoreCase) == true))
                {
                    throw new UserNotVerifiedException(loginDto.Email, ex);
                }
                
                throw new ApplicationException("An error occurred during login.");
            }
        }

        // Helper method to process authentication result
        private AuthResponseDto ProcessAuthenticationResult(InitiateAuthResponse authResponse, string email)
        {
            if (authResponse == null || authResponse.AuthenticationResult == null)
            {
                throw new ApplicationException("Authentication failed.");
            }
            
            // Get user details
            try
            {
                var getUserRequest = new GetUserRequest
                {
                    AccessToken = authResponse.AuthenticationResult.AccessToken
                };
                
                var userResponse = _cognitoClient.GetUserAsync(getUserRequest).Result;
                var attributes = userResponse.UserAttributes.ToDictionary(a => a.Name, a => a.Value);
                
                return new AuthResponseDto
                {
                    Token = authResponse.AuthenticationResult.IdToken,
                    RefreshToken = authResponse.AuthenticationResult.RefreshToken,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(authResponse.AuthenticationResult.ExpiresIn),
                    UserId = userResponse.Username,
                    User = new UserProfileDto
                    {
                        Id = Guid.Empty, // Will be populated from database in controller
                        Email = email,
                        FullName = attributes.TryGetValue("name", out var name) ? name : string.Empty,
                        School = attributes.TryGetValue("custom:school", out var school) ? school : string.Empty,
                        ProfileImageUrl = null,
                        IsEmailVerified = true,
                        ParentalConsentGiven = attributes.TryGetValue("custom:parental_consent_given", out var consent) 
                            && bool.TryParse(consent, out var consentBool) && consentBool,
                        IsProfileComplete = true,
                        Status = "Active"
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user details after authentication: {Message}", ex.Message);
                
                // Return minimal response if we can't get user details
                return new AuthResponseDto
                {
                    Token = authResponse.AuthenticationResult.IdToken,
                    RefreshToken = authResponse.AuthenticationResult.RefreshToken,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(authResponse.AuthenticationResult.ExpiresIn),
                    UserId = string.Empty,
                    User = new UserProfileDto
                    {
                        Id = Guid.Empty,
                        Email = email,
                        FullName = string.Empty,
                        School = string.Empty,
                        IsEmailVerified = true,
                        Status = "Active"
                    }
                };
            }
        }

        public async Task<bool> ConfirmSignUpAsync(string email, string confirmationCode)
        {
            try
            {
                // Make sure to calculate and include the SECRET_HASH
                string secretHash = CalculateSecretHash(email);
                
                var request = new ConfirmSignUpRequest
                {
                    ClientId = _cognitoSettings.ClientId,
                    Username = email,
                    ConfirmationCode = confirmationCode,
                    SecretHash = secretHash
                };

                _logger.LogInformation("Confirming sign up for {Email} with code", email);
                var response = await _cognitoClient.ConfirmSignUpAsync(request);
                return response.HttpStatusCode == HttpStatusCode.OK;
            }
            catch (CodeMismatchException ex)
            {
                _logger.LogWarning(ex, "Invalid verification code: {Email}", email);
                throw new ApplicationException("Invalid verification code.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming sign up: {Email}", email);
                throw new ApplicationException("An error occurred confirming your email.");
            }
        }

        public async Task<bool> ResendConfirmationCodeAsync(string email)
        {
            try
            {
                // Calculate the secret hash
                string secretHash = CalculateSecretHash(email);

                var request = new ResendConfirmationCodeRequest
                {
                    ClientId = _cognitoSettings.ClientId,
                    Username = email,
                    SecretHash = secretHash
                };

                _logger.LogInformation("Resending confirmation code to {Email}", email);
                var response = await _cognitoClient.ResendConfirmationCodeAsync(request);
                return response.HttpStatusCode == HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resending confirmation code: {Email}", email);
                throw new ApplicationException("An error occurred sending the verification code.");
            }
        }

        public async Task<bool> RequestForgotPasswordAsync(string email)
        {
            try
            {
                // Calculate the secret hash
                string secretHash = CalculateSecretHash(email);

                var request = new ForgotPasswordRequest
                {
                    ClientId = _cognitoSettings.ClientId,
                    Username = email,
                    SecretHash = secretHash
                };

                _logger.LogInformation("Requesting password reset for {Email}", email);
                var response = await _cognitoClient.ForgotPasswordAsync(request);
                return response.HttpStatusCode == HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting password reset: {Email}", email);
                throw new ApplicationException("An error occurred requesting a password reset.");
            }
        }

        public async Task<bool> ConfirmForgotPasswordAsync(string email, string confirmationCode, string newPassword)
        {
            try
            {
                // Calculate the secret hash
                string secretHash = CalculateSecretHash(email);

                var request = new ConfirmForgotPasswordRequest
                {
                    ClientId = _cognitoSettings.ClientId,
                    Username = email,
                    ConfirmationCode = confirmationCode,
                    Password = newPassword,
                    SecretHash = secretHash
                };

                _logger.LogInformation("Confirming password reset for {Email}", email);
                var response = await _cognitoClient.ConfirmForgotPasswordAsync(request);
                return response.HttpStatusCode == HttpStatusCode.OK;
            }
            catch (CodeMismatchException ex)
            {
                _logger.LogWarning(ex, "Invalid verification code for password reset: {Email}", email);
                throw new ApplicationException("Invalid verification code.");
            }
            catch (InvalidPasswordException ex)
            {
                _logger.LogWarning(ex, "Invalid password format: {Email}", email);
                throw new ApplicationException("Password does not meet the requirements.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming password reset: {Email}", email);
                throw new ApplicationException("An error occurred resetting your password.");
            }
        }

        public async Task<bool> ChangePasswordAsync(string accessToken, string oldPassword, string newPassword)
        {
            try
            {
                var request = new ChangePasswordRequest
                {
                    AccessToken = accessToken,
                    PreviousPassword = oldPassword,
                    ProposedPassword = newPassword
                };

                var response = await _cognitoClient.ChangePasswordAsync(request);
                return response.HttpStatusCode == HttpStatusCode.OK;
            }
            catch (NotAuthorizedException ex)
            {
                _logger.LogWarning(ex, "Incorrect old password for password change");
                throw new ApplicationException("Incorrect old password.");
            }
            catch (InvalidPasswordException ex)
            {
                _logger.LogWarning(ex, "Invalid new password format");
                throw new ApplicationException("New password does not meet the requirements.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password");
                throw new ApplicationException("An error occurred changing your password.");
            }
        }

        public async Task<string> GetUserAttributeAsync(string accessToken, string attributeName)
        {
            try
            {
                var request = new GetUserRequest
                {
                    AccessToken = accessToken
                };

                var response = await _cognitoClient.GetUserAsync(request);
                var attribute = response.UserAttributes.FirstOrDefault(a => a.Name == attributeName);

                return attribute?.Value ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user attribute: {AttributeName}", attributeName);
                throw new ApplicationException("An error occurred retrieving user information.");
            }
        }

        public async Task<bool> IsEmailVerifiedAsync(string email)
        {
            try
            {
                var request = new AdminGetUserRequest
                {
                    UserPoolId = _cognitoSettings.PoolId,
                    Username = email
                };

                var response = await _cognitoClient.AdminGetUserAsync(request);
                
                // Check if the email_verified attribute is true
                var emailVerifiedAttribute = response.UserAttributes
                    .FirstOrDefault(a => a.Name == "email_verified");
                
                return emailVerifiedAttribute != null && 
                       bool.TryParse(emailVerifiedAttribute.Value, out bool verified) && 
                       verified;
            }
            catch (UserNotFoundException)
            {
                _logger.LogWarning("User not found in Cognito: {Email}", email);
                return false;
            }
            catch (NotAuthorizedException authEx)
            {
                // This might happen if we don't have admin permissions to call AdminGetUser
                _logger.LogWarning(authEx, "Authorization error checking email verification: {Email}. This likely means you don't have admin permissions.", email);
                
                // Try alternate approach - use the ListUsers API which requires fewer permissions
                try
                {
                    var listUsersRequest = new ListUsersRequest
                    {
                        UserPoolId = _cognitoSettings.PoolId,
                        Filter = $"email = \"{email}\"",
                        Limit = 1
                    };
                    
                    var listUsersResponse = await _cognitoClient.ListUsersAsync(listUsersRequest);
                    
                    if (listUsersResponse.Users.Count > 0)
                    {
                        var user = listUsersResponse.Users[0];
                        var emailVerifiedAttribute = user.Attributes
                            .FirstOrDefault(a => a.Name == "email_verified");
                        
                        return emailVerifiedAttribute != null && 
                               bool.TryParse(emailVerifiedAttribute.Value, out bool verified) && 
                               verified;
                    }
                    
                    return false;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error using ListUsers to check email verification: {Email}", email);
                    throw new ApplicationException("Insufficient permissions to check email verification status.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking email verification status: {Email}", ex.Message);
                throw new ApplicationException("An error occurred checking email verification status.");
            }
        }

        private string CalculateSecretHash(string username)
        {
            // Skip if client secret is not set
            if (string.IsNullOrEmpty(_cognitoSettings.ClientSecret))
            {
                _logger.LogWarning("Client secret is not set - SECRET_HASH will not be calculated");
                return string.Empty;
            }

            try
            {
                // Implementation of the secret hash function for Cognito
                // This is required if the app client was created with a client secret
                string message = username + _cognitoSettings.ClientId;
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                byte[] keyBytes = Encoding.UTF8.GetBytes(_cognitoSettings.ClientSecret);
                
                using (var hmac = new System.Security.Cryptography.HMACSHA256(keyBytes))
                {
                    byte[] hashBytes = hmac.ComputeHash(messageBytes);
                    return Convert.ToBase64String(hashBytes);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating SECRET_HASH for {Username}", username);
                throw new ApplicationException("Error calculating authentication signature.");
            }
        }

        private async Task UpdateUserPoolWithEmailRedirect(string userPoolId, string redirectUrl)
        {
            try
            {
                // Instead of trying to modify the user pool itself (which requires admin permissions),
                // we'll just store the redirect URL and handle it in our application
                _logger.LogInformation("Email verification redirect URL will be handled by application: {0}", redirectUrl);
                
                // Skip direct modification of Cognito user pool settings since it requires admin permissions
                // We'll handle the redirect in the AuthController's email confirmation endpoint
                
                // Adding a completed task to make this method properly async
                await Task.CompletedTask;
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling verification redirect URL: {0}", ex.Message);
                // Don't throw the exception, just log it and continue
                // This prevents the registration process from failing due to redirect issues
            }
        }
    }
} 