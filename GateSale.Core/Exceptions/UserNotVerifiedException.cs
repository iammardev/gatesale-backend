using System;

namespace GateSale.Core.Exceptions
{
    public class UserNotVerifiedException : Exception
    {
        public string Email { get; }

        public UserNotVerifiedException(string email) 
            : base($"User {email} is not verified")
        {
            Email = email;
        }
        
        public UserNotVerifiedException(string email, Exception innerException) 
            : base($"User {email} is not verified", innerException)
        {
            Email = email;
        }
    }
} 