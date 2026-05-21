namespace SocialHub.Notification.Application.Exceptions;

public sealed class AppException : Exception
{
    public AppException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }

    public static AppException BadRequest(string message) => new(400, message);
    public static AppException Unauthorized(string message) => new(401, message);
    public static AppException Forbidden(string message) => new(403, message);
    public static AppException NotFound(string message) => new(404, message);
}
