using System.Security.Claims;
using Docly.Api.Infrastructure;
using Docly.App.Interfaces;
using Docly.App.Services;
using Microsoft.AspNetCore.Mvc;

namespace Docly.Api.Endpoints
{
    public static class DocumentEndpoints
    {
        public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/documents")
                .RequireAuthorization()
                .RequireRateLimiting(RateLimitPolicies.Standard)
                .DisableAntiforgery() // JWT bearer auth, no cookies — antiforgery doesn't apply
                .WithTags("Documents");

            group.MapPost("/", async (IFormFile? file, [FromForm] string? title, IDocumentService documentService, ClaimsPrincipal user, CancellationToken cancellationToken) =>
            {
                if (file is null || file.Length == 0)
                    return Results.Problem(detail: "A file is required.", statusCode: StatusCodes.Status400BadRequest);

                await using var stream = file.OpenReadStream();
                var document = await documentService.UploadAsync(
                    user.GetUserId(),
                    string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(file.FileName) : title,
                    file.FileName,
                    stream,
                    file.Length,
                    cancellationToken);

                return Results.Created($"/api/documents/{document.Id}", ApiDtos.Document(document));
            });

            group.MapGet("/", async (IDocumentRepository documents, ClaimsPrincipal user, CancellationToken cancellationToken) =>
            {
                var allDocuments = await documents.GetAllAsync(user.GetUserId(), cancellationToken);
                return Results.Ok(allDocuments.Select(ApiDtos.Document));
            });

            group.MapGet("/{id:guid}", async (Guid id, IDocumentRepository documents, ClaimsPrincipal user, CancellationToken cancellationToken) =>
            {
                var document = await documents.GetByIdAsync(id, user.GetUserId(), cancellationToken);
                return document is null ? Results.NotFound() : Results.Ok(ApiDtos.Document(document));
            });

            group.MapDelete("/{id:guid}", async (Guid id, IDocumentService documentService, ClaimsPrincipal user, CancellationToken cancellationToken) =>
            {
                await documentService.DeleteAsync(user.GetUserId(), id, cancellationToken);
                return Results.NoContent();
            });

            // Re-queues a document whose processing failed (e.g. Ollama was down at the time).
            group.MapPost("/{id:guid}/retry", async (Guid id, IDocumentService documentService, ClaimsPrincipal user, CancellationToken cancellationToken) =>
            {
                var document = await documentService.RetryAsync(user.GetUserId(), id, cancellationToken);
                return Results.Ok(ApiDtos.Document(document));
            });

            return app;
        }
    }
}
