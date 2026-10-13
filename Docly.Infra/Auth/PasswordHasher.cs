using Docly.App.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace Docly.Infra.Auth
{
    public class PasswordHasher : IPasswordHasher
    {
        private readonly PasswordHasher<object> _hasher = new();

        // A real hash of a random password, verified against when the user doesn't exist,
        // so failed logins take the same time whether or not the email is registered.
        private static readonly string DummyHash = new PasswordHasher<object>().HashPassword(new object(), Guid.NewGuid().ToString());

        public string Hash(string password)
        {
            return _hasher.HashPassword(new object(), password);
        }

        public bool Verify(string? hashedPassword, string providedPassword)
        {
            var result = _hasher.VerifyHashedPassword(new object(), hashedPassword ?? DummyHash, providedPassword ?? string.Empty);
            return hashedPassword is not null
                && result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
        }
    }
}
