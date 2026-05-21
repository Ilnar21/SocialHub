namespace SocialHub.Notification.Application.Exceptions;

public sealed class AppException : Exception
{
    public AppException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }

    public static AppException Unauthorized(string message) => new(401, message);
}
