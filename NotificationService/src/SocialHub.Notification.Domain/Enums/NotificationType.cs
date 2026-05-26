namespace SocialHub.Notification.Domain.Enums;

public enum NotificationType
{
    MessageReceived = 1,
    SuggestedPostCreated = 2,
    SuggestedPostApproved = 3,
    SuggestedPostRejected = 4,
    ReportResolved = 5,
    PublicationDecision = 6,
    UserBlocked = 7,
    JoinRequestCreated = 8,
    JoinRequestApproved = 9,
    JoinRequestRejected = 10,
    UserRegistered = 11,
    UserUnblocked = 12,
    CommunityBlocked = 13,
    CommunityUnblocked = 14
}
