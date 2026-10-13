namespace Docly.App.Interfaces
{
    public interface IPasswordHasher
    {
        string Hash(string password);

        /// <summary>
        /// Verifies a password. Pass <c>null</c> for <paramref name="hashedPassword"/> when the user doesn't
        /// exist — the check still runs so response time doesn't reveal which emails are registered.
        /// </summary>
        bool Verify(string? hashedPassword, string providedPassword);
    }
}
