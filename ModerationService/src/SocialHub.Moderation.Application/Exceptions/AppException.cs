namespace SocialHub.Moderation.Application.Exceptions;

public sealed class AppException : Exception
{
    private AppException(int statusCode, string code, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public int StatusCode { get; }
    public string Code { get; }

    public static AppException BadRequest(string message) => new(400, "bad_request", message);
    public static AppException Unauthorized(string message) => new(401, "unauthorized", message);
    public static AppException Forbidden(string message) => new(403, "forbidden", message);
    public static AppException NotFound(string message) => new(404, "not_found", message);
}
