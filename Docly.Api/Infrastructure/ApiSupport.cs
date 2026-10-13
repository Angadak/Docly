using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Docly.Api.Infrastructure;

public static class RateLimitPolicies
{
    public const string Standard = "standard";
    public const string AiHeavy = "ai-heavy";
    public const string Auth = "auth";
}

public static class ClaimsPrincipalExtensions
{
    /// <summary>The signed-in user's id from the JWT "sub" claim.</summary>
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(sub, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("The access token has no valid subject.");
    }
}

public static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var body = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(
                e => e.Key,
                e => new { status = e.Value.Status.ToString(), description = e.Value.Description })
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
    }
}

/// <summary>Response shapes shared by several endpoints.</summary>
public static class ApiDtos
{
    public static object Document(Docly.Domain.Entities.Document d) => new
    {
        d.Id,
        d.Title,
        d.Description,
        d.Filename,
        d.ContentType,
        d.FileSizeBytes,
        CreatedAtUtc = AsUtc(d.CreatedAtUtc),
        Status = d.Status.ToString(),
        d.ProcessingError
    };

    public static object Conversation(Docly.Domain.Entities.ChatConversation c) => new
    {
        c.Id,
        c.Title,
        c.Tags,
        CreatedAtUtc = AsUtc(c.CreatedAtUtc),
        UpdatedAtUtc = AsUtc(c.UpdatedAtUtc)
    };

    public static object Message(Docly.Domain.Entities.ChatMessage m) => new
    {
        m.Id,
        m.ConversationId,
        m.Role,
        m.Content,
        m.SourceDocuments,
        CreatedAtUtc = AsUtc(m.CreatedAtUtc)
    };

    // Serialize with a "Z" suffix so clients never mistake UTC for local time.
    private static DateTimeOffset AsUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
