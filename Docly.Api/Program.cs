using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Docly.Api.Endpoints;
using Docly.Api.Infrastructure;
using Docly.Infra;
using Docly.Infra.DbContexts;
using Docly.Infra.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDoclyInfrastructure(builder.Configuration);

// ---- Error handling: every failure becomes an RFC 7807 problem+json response ----
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// ---- Authentication ----
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddOpenApi();

// ---- Upload size: reject oversized bodies before they are buffered ----
const long MaxRequestBodyBytes = 26 * 1024 * 1024; // 25 MB file + multipart overhead
builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = MaxRequestBodyBytes);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = MaxRequestBodyBytes);

// ---- CORS: Docly.Web (Blazor WASM) is served from its own origin ----
const string WebClientCorsPolicy = "DoclyWebClient";
var webClientOrigins = builder.Configuration.GetSection("WebClientOrigins").Get<string[]>()
    ?? new[] { "https://localhost:7201", "http://localhost:5201" };

builder.Services.AddCors(options =>
{
    options.AddPolicy(WebClientCorsPolicy, policy =>
        policy.WithOrigins(webClientOrigins).AllowAnyHeader().AllowAnyMethod());
});

// ---- Rate limiting: partitioned per user (or per IP when anonymous), not one bucket for everyone ----
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    string PartitionKey(HttpContext context) =>
        context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? context.Connection.RemoteIpAddress?.ToString()
        ?? "anonymous";

    void AddPerCallerPolicy(RateLimiterOptions options, string name, int permitsPerMinute) =>
        options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
            PartitionKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    AddPerCallerPolicy(options, RateLimitPolicies.Standard, 60);
    AddPerCallerPolicy(options, RateLimitPolicies.AiHeavy, 10);
    AddPerCallerPolicy(options, RateLimitPolicies.Auth, 10);
});

var app = builder.Build();

// Opt-in (on in Development and in docker-compose): create/upgrade the schema on startup.
// Production deployments can instead apply migrations from CI (dotnet ef migrations bundle).
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    app.Logger.LogInformation("Applying database migrations…");
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // serves /openapi/v1.json
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Docly API v1");
        options.RoutePrefix = "swagger";
    });
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

// Behind a TLS-terminating proxy or in a plain-HTTP container, set UseHttpsRedirection=false.
if (app.Configuration.GetValue("UseHttpsRedirection", true))
    app.UseHttpsRedirection();

app.UseCors(WebClientCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// This is a pure API — the UI is served separately by the Docly.Web Blazor project.
app.MapAuthEndpoints();
app.MapDocumentEndpoints();
app.MapSearchEndpoints();
app.MapChatEndpoints();
app.MapChatConversationEndpoints();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthResponseWriter.WriteAsync
}).ExcludeFromDescription();

app.Run();

/// <summary>Exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program;
