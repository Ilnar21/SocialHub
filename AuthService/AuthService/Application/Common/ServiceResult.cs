namespace AuthService.Application.Common;

public sealed class ServiceResult<T>
{
    private ServiceResult(T? value, string? errorCode, string? errorMessage)
    {
        Value = value;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public T? Value { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }
    public bool IsSuccess => ErrorCode is null;

    public static ServiceResult<T> Success(T value) => new(value, null, null);

    public static ServiceResult<T> Failure(string code, string message) => new(default, code, message);
}
