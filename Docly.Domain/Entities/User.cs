using System.Net.Mail;

namespace Docly.Domain.Entities
{
    public class User
    {
        public const int MaxEmailLength = 320; // RFC 5321

        public Guid Id { get; private set; }
        public string Email { get; private set; } = null!;
        public string PasswordHash { get; private set; } = null!;
        public DateTime CreatedAtUtc { get; private set; }

        private User() { }

        public User(string email, string passwordHash)
        {
            if (!IsValidEmail(email))
                throw new ArgumentException("Email must be a valid email address.", nameof(email));
            if (string.IsNullOrWhiteSpace(passwordHash))
                throw new ArgumentException("Password hash cannot be empty.", nameof(passwordHash));

            Id = Guid.NewGuid();
            Email = NormalizeEmail(email);
            PasswordHash = passwordHash;
            CreatedAtUtc = DateTime.UtcNow;
        }

        public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

        public static bool IsValidEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            var trimmed = email.Trim();
            return trimmed.Length <= MaxEmailLength
                && MailAddress.TryCreate(trimmed, out var address)
                && address.Address == trimmed;
        }
    }
}
