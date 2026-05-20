using SocialHub.Auth.Domain.Entities;

namespace SocialHub.Auth.Application.Abstractions;

public interface ITokenService
{
    AuthSession CreateSession(UserAccount user);
}
