namespace Docly.App.Exceptions;

/// <summary>The requested resource does not exist or does not belong to the caller (mapped to 404).</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

/// <summary>The request conflicts with the current state of the resource (mapped to 409).</summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}

/// <summary>The request is invalid (mapped to 400). Domain guards throw <see cref="ArgumentException"/>, which maps the same way.</summary>
public sealed class RequestValidationException : Exception
{
    public RequestValidationException(string message) : base(message) { }
}
