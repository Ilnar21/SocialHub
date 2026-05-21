namespace SocialHub.Feed.Application.Exceptions;

/// <summary>
/// Базовое исключение прикладного уровня. Несёт HTTP-статус,
/// который преобразуется в ответ ExceptionHandlingMiddleware.
/// </summary>
public class AppException : Exception
{
    public int StatusCode { get; }

    public AppException(string message, int statusCode = 400)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
