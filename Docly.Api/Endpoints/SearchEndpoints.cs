using System.Security.Claims;
using Docly.Api.Infrastructure;
using Docly.App.Interfaces;
using Docly.App.Options;
using Microsoft.Extensions.Options;

namespace Docly.Api.Endpoints;

public static class SearchEndpoints
{
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/search")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitPolicies.AiHeavy)
            .WithTags("Search");

        group.MapGet("/", async (string? query, int? topN, IChunkSearchService searchService, IOptions<RagOptions> rag, ClaimsPrincipal user, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(query))
                return Results.Problem(detail: "Query parameter 'query' is required.", statusCode: StatusCodes.Status400BadRequest);

            var effectiveTopN = Math.Clamp(topN ?? rag.Value.DefaultTopN, 1, 20);
            var results = await searchService.SearchAsync(query, user.GetUserId(), effectiveTopN, 0, cancellationToken);

            return Results.Ok(new { Query = query, ResultCount = results.Count, Results = results });
        });

        return app;
    }
}
