using GateSale.Core.DTOs;
using GateSale.Core.Entities;

namespace GateSale.Core.Interfaces
{
    public interface ICognitoService
    {
        Task<AuthResponseDto> RegisterUserAsync(RegisterDto registerDto);
        Task<AuthResponseDto> LoginAsync(LoginDto loginDto);
        Task<bool> ConfirmSignUpAsync(string email, string confirmationCode);
        Task<bool> ResendConfirmationCodeAsync(string email);
        Task<bool> RequestForgotPasswordAsync(string email);
        Task<bool> ConfirmForgotPasswordAsync(string email, string confirmationCode, string newPassword);
        Task<bool> ChangePasswordAsync(string accessToken, string oldPassword, string newPassword);
        Task<string> GetUserAttributeAsync(string accessToken, string attributeName);
        Task<bool> IsEmailVerifiedAsync(string email);
    }
} 