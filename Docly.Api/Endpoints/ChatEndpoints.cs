using System.Security.Claims;
using Docly.Api.Infrastructure;
using Docly.App.Services;

namespace Docly.Api.Endpoints
{
    public sealed record ChatRequest(string? Question, Guid? ConversationId = null, int? TopN = null);

    public static class ChatEndpoints
    {
        public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/chat")
                .RequireAuthorization()
                .RequireRateLimiting(RateLimitPolicies.AiHeavy)
                .WithTags("Chat");

            group.MapPost("/", async (ChatRequest request, IChatService chatService, ClaimsPrincipal user, CancellationToken cancellationToken) =>
            {
                var result = await chatService.AskAsync(
                    user.GetUserId(),
                    request.Question ?? string.Empty,
                    request.ConversationId,
                    request.TopN,
                    cancellationToken);

                return Results.Ok(new
                {
                    result.ConversationId,
                    result.Question,
                    result.Answer,
                    SourceChunks = result.Sources
                });
            });

            return app;
        }
    }
}
