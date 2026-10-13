using Docly.Api.Infrastructure;
using Docly.App.Interfaces;
using Docly.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Docly.Api.Endpoints
{
    public sealed record RegisterRequest(string? Email, string? Password);
    public sealed record LoginRequest(string? Email, string? Password);

    public static class AuthEndpoints
    {
        private const int MinPasswordLength = 8;
        private const int MaxPasswordLength = 128;

        public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/auth")
                .RequireRateLimiting(RateLimitPolicies.Auth)
                .WithTags("Auth");

            group.MapPost("/register", async (RegisterRequest request, IUserRepository users, IPasswordHasher passwordHasher, IJwtTokenService tokenService, ILogger<Program> logger, CancellationToken cancellationToken) =>
            {
                if (!User.IsValidEmail(request.Email))
                    return Problem(StatusCodes.Status400BadRequest, "Enter a valid email address.");

                if (string.IsNullOrEmpty(request.Password) || request.Password.Length < MinPasswordLength)
                    return Problem(StatusCodes.Status400BadRequest, $"Password must be at least {MinPasswordLength} characters.");

                if (request.Password.Length > MaxPasswordLength)
                    return Problem(StatusCodes.Status400BadRequest, $"Password must be at most {MaxPasswordLength} characters.");

                if (await users.GetByEmailAsync(request.Email!, cancellationToken) is not null)
                    return Problem(StatusCodes.Status409Conflict, "An account with this email already exists.");

                var user = new User(request.Email!, passwordHasher.Hash(request.Password));
                await users.AddAsync(user, cancellationToken);

                try
                {
                    await users.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
                {
                    // Two registrations for the same email raced past the check above.
                    return Problem(StatusCodes.Status409Conflict, "An account with this email already exists.");
                }

                logger.LogInformation("New user registered: {UserId}", user.Id);

                return Results.Created($"/api/users/{user.Id}", new
                {
                    user.Id,
                    user.Email,
                    Token = tokenService.GenerateToken(user)
                });
            });

            group.MapPost("/login", async (LoginRequest request, IUserRepository users, IPasswordHasher passwordHasher, IJwtTokenService tokenService, ILogger<Program> logger, CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
                    return Problem(StatusCodes.Status400BadRequest, "Email and password are required.");

                var user = await users.GetByEmailAsync(request.Email, cancellationToken);

                // Always run the hash check so timing doesn't reveal whether the email exists.
                if (!passwordHasher.Verify(user?.PasswordHash, request.Password) || user is null)
                {
                    logger.LogWarning("Failed login attempt");
                    return Problem(StatusCodes.Status401Unauthorized, "Incorrect email or password.");
                }

                logger.LogInformation("Successful login for user {UserId}", user.Id);

                return Results.Ok(new
                {
                    user.Id,
                    user.Email,
                    Token = tokenService.GenerateToken(user)
                });
            });

            return app;
        }

        private static IResult Problem(int status, string detail) => Results.Problem(detail: detail, statusCode: status);
    }
}
