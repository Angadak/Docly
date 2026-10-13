using Docly.App.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Docly.Api.Infrastructure;

/// <summary>
/// Turns exceptions into problem+json responses with the right status code, so invalid input is a 400
/// (not a 500) and internal details never reach the client.
/// </summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    private const int ClientClosedRequest = 499;

    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // The client went away; nothing to send and nothing to alert on.
            httpContext.Response.StatusCode = ClientClosedRequest;
            return true;
        }

        var (status, title, detail) = exception switch
        {
            RequestValidationException e => (StatusCodes.Status400BadRequest, "Invalid request", e.Message),
            ArgumentException e => (StatusCodes.Status400BadRequest, "Invalid request", StripParameterName(e)),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized", "Please sign in again."),
            NotFoundException e => (StatusCodes.Status404NotFound, "Not found", e.Message),
            ConflictException e => (StatusCodes.Status409Conflict, "Conflict", e.Message),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                (StatusCodes.Status409Conflict, "Conflict", "This item already exists."),
            EmbeddingGenerationException or AnswerGenerationException =>
                (StatusCodes.Status503ServiceUnavailable, "AI service unavailable", "The AI service is currently unavailable. Make sure Ollama is running and the models are pulled."),
            BadHttpRequestException e => (e.StatusCode, "Bad request", e.StatusCode == StatusCodes.Status413PayloadTooLarge
                ? "The upload is too large."
                : "The request could not be read."),
            _ => (StatusCodes.Status500InternalServerError, "Server error", "An unexpected error occurred. Please try again later.")
        };

        if (status >= 500)
            _logger.LogError(exception, "Request {Method} {Path} failed", httpContext.Request.Method, httpContext.Request.Path);
        else
            _logger.LogDebug(exception, "Request {Method} {Path} rejected with {Status}", httpContext.Request.Method, httpContext.Request.Path, status);

        if (httpContext.Response.HasStarted)
            return false;

        httpContext.Response.StatusCode = status;
        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail }
        });
    }

    // "Title cannot be empty. (Parameter 'title')" -> "Title cannot be empty."
    private static string StripParameterName(ArgumentException exception)
    {
        var message = exception.Message;
        var index = message.IndexOf(" (Parameter '", StringComparison.Ordinal);
        return index > 0 ? message[..index] : message;
    }
}
