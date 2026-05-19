using AuthService.Application.Dtos;
using AuthService.Domain.Entities;

namespace AuthService.Application.Mapping;

public static class UserMapping
{
    public static UserResponse ToResponse(this UserAccount user) =>
        new(
            user.Id,
            user.Username,
            user.Email,
            user.Role,
            user.Status,
            user.BlockReason,
            user.BlockedUntil,
            user.CreatedAt,
            user.UpdatedAt,
            new ProfileResponse(user.Profile.DisplayName, user.Profile.Bio, user.Profile.AvatarUrl));
}
