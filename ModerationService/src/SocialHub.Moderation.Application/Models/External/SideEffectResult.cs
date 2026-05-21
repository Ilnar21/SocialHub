namespace SocialHub.Moderation.Application.Models.External;

public sealed record SideEffectResult(
    string ServiceName,
    string RequestPath,
    bool Succeeded,
    string? ErrorMessage)
{
    public static SideEffectResult Success(string serviceName, string requestPath) => new(serviceName, requestPath, true, null);
    public static SideEffectResult Failed(string serviceName, string requestPath, string errorMessage) => new(serviceName, requestPath, false, errorMessage);
}
