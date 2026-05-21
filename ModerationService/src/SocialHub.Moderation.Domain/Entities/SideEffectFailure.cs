namespace SocialHub.Moderation.Domain.Entities;

public sealed class SideEffectFailure
{
    public SideEffectFailure(
        Guid id,
        string action,
        string targetType,
        string targetId,
        string serviceName,
        string requestPath,
        string errorMessage,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        Action = action;
        TargetType = targetType;
        TargetId = targetId;
        ServiceName = serviceName;
        RequestPath = requestPath;
        ErrorMessage = errorMessage;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; }
    public string Action { get; }
    public string TargetType { get; }
    public string TargetId { get; }
    public string ServiceName { get; }
    public string RequestPath { get; }
    public string ErrorMessage { get; }
    public DateTimeOffset CreatedAtUtc { get; }
}
