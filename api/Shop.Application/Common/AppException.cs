namespace Shop.Application.Common;

/// <summary>An expected failure that the API turns into a ProblemDetails response with a stable code.</summary>
public sealed class AppException(int status, string code, string message, IDictionary<string, string[]>? errors = null)
    : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public IDictionary<string, string[]>? Errors { get; } = errors;

    public static AppException Unauthorized(string message = "Please sign in again.") =>
        new(401, ErrorCodes.Unauthorized, message);

    public static AppException NotFound(string message = "Not found.") => new(404, ErrorCodes.NotFound, message);

    public static AppException Conflict(string code, string message, IDictionary<string, string[]>? errors = null) =>
        new(409, code, message, errors);
}
