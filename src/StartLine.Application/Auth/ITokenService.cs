using StartLine.Domain.Users;

namespace StartLine.Application.Auth;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    TimeSpan RefreshTokenLifetime { get; }
}
