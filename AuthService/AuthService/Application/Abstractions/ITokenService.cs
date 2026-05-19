using AuthService.Domain.Entities;

namespace AuthService.Application.Abstractions;

public interface ITokenService
{
    AuthSession CreateSession(UserAccount user);
}
