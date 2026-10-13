using System.Security.Claims;
using Docly.Api.Infrastructure;
using Docly.App.Exceptions;
using Docly.App.Interfaces;
using Docly.Domain.Entities;

namespace Docly.Api.Endpoints
{
    public sealed record CreateConversationRequest(string? Title);
    public sealed record UpdateConversationRequest(string? Title = null, List<string>? Tags = null);

    public static class ChatConversationEndpoints
    {
        private const int MaxPageSize = 200;

        public static IEndpointRouteBuilder MapChatConversationEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/conversations")
                .RequireAuthorization()
                .RequireRateLimiting(RateLimitPolicies.Standard)
                .WithTags("Conversations");

            // GET /api/conversations?skip=0&take=50 - List the user's conversations, most recent first
            group.MapGet("/", async (int? skip, int? take, IChatConversationRepository conversations, ClaimsPrincipal user, CancellationToken cancellationToken) =>
            {
                var page = await conversations.GetByUserIdAsync(
                    user.GetUserId(),
                    Math.Max(skip ?? 0, 0),
                    Math.Clamp(take ?? 100, 1, MaxPageSize),
                    cancellationToken);

                return Results.Ok(page.Select(ApiDtos.Conversation));
            });

            // GET /api/conversations/search?q=query - Search conversation titles
            group.MapGet("/search", async (string? q, IChatConversationRepository conversations, ClaimsPrincipal user, CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(q))
                    return Results.Problem(detail: "Query parameter 'q' is required.", statusCode: StatusCodes.Status400BadRequest);

                var results = await conversations.SearchByUserIdAsync(user.GetUserId(), q, MaxPageSize, cancellationToken);
                return Results.Ok(results.Select(ApiDtos.Conversation));
            });

            // POST /api/conversations - Create an empty conversation
            group.MapPost("/", async (CreateConversationRequest request, IChatConversationRepository conversations, ClaimsPrincipal user, CancellationToken cancellationToken) =>
            {
                var conversation = new ChatConversation(user.GetUserId(), request.Title ?? string.Empty);

                await conversations.AddAsync(conversation, cancellationToken);
                await conversations.SaveChangesAsync(cancellationToken);

                return Results.Created($"/api/conversations/{conversation.Id}", ApiDtos.Conversation(conversation));
            });

            // GET /api/conversations/{id}
            group.MapGet("/{id:guid}", async (Guid id, IChatConversationRepository conversations, ClaimsPrincipal user, CancellationToken cancellationToken) =>
            {
                var conversation = await GetOwnedAsync(conversations, id, user, cancellationToken);
                return Results.Ok(ApiDtos.Conversation(conversation));
            });

            // PATCH /api/conversations/{id} - Rename and/or replace tags
            group.MapPatch("/{id:guid}", async (Guid id, UpdateConversationRequest request, IChatConversationRepository conversations, ClaimsPrincipal user, CancellationToken cancellationToken) =>
            {
                var conversation = await GetOwnedAsync(conversations, id, user, cancellationToken);

                if (request.Title is not null)
                    conversation.UpdateTitle(request.Title);

                if (request.Tags is not null)
                {
                    conversation.ClearTags();
                    foreach (var tag in request.Tags)
                        conversation.AddTag(tag);
                }

                await conversations.SaveChangesAsync(cancellationToken);
                return Results.Ok(ApiDtos.Conversation(conversation));
            });

            // DELETE /api/conversations/{id} - Delete a conversation and its messages
            group.MapDelete("/{id:guid}", async (Guid id, IChatConversationRepository conversations, ClaimsPrincipal user, CancellationToken cancellationToken) =>
            {
                var conversation = await GetOwnedAsync(conversations, id, user, cancellationToken);

                conversations.Remove(conversation);
                await conversations.SaveChangesAsync(cancellationToken);

                return Results.NoContent();
            });

            // GET /api/conversations/{id}/messages?skip=0&take=200 - Messages, oldest first.
            // Messages are written only by POST /api/chat, so clients can't forge assistant replies.
            group.MapGet("/{id:guid}/messages", async (Guid id, int? skip, int? take, IChatConversationRepository conversations, IChatMessageRepository messages, ClaimsPrincipal user, CancellationToken cancellationToken) =>
            {
                await GetOwnedAsync(conversations, id, user, cancellationToken);

                var page = await messages.GetByConversationIdAsync(
                    id,
                    Math.Max(skip ?? 0, 0),
                    Math.Clamp(take ?? MaxPageSize, 1, MaxPageSize),
                    cancellationToken);

                return Results.Ok(page.Select(ApiDtos.Message));
            });

            return app;
        }

        // Someone else's conversation is reported as "not found", not "forbidden", so ids can't be probed.
        private static async Task<ChatConversation> GetOwnedAsync(IChatConversationRepository conversations, Guid id, ClaimsPrincipal user, CancellationToken cancellationToken) =>
            await conversations.GetForUserAsync(id, user.GetUserId(), cancellationToken)
                ?? throw new NotFoundException("Conversation not found.");
    }
}
