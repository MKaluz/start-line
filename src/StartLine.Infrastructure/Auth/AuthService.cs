using Microsoft.Extensions.Logging;
using StartLine.Application.Auth;
using StartLine.Application.Users;
using StartLine.Domain.Users;

namespace StartLine.Infrastructure.Auth;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthService> _logger;

    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public AuthService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        ITokenService tokenService,
        ILogger<AuthService> logger)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (await _users.ExistsByEmailAsync(request.Email, ct))
            throw new DuplicateEmailException(request.Email);

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var user = User.Create(request.Email, passwordHash, Role.Athlete);

        await _users.AddAsync(user, ct);
        await _users.SaveChangesAsync(ct);

        _logger.LogInformation("User registered: {Email}", Sanitize(request.Email));

        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _users.FindByEmailAsync(request.Email, ct);

        if (user == null)
        {
            _logger.LogWarning("Failed login attempt for unknown email: {Email}", Sanitize(request.Email));
            throw new InvalidCredentialsException();
        }

        if (user.IsLockedOut())
            throw new AccountLockedException();

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            user.RecordFailedLogin();

            if (user.FailedLoginCount >= MaxFailedAttempts)
            {
                user.LockUntil(DateTimeOffset.UtcNow.Add(LockoutDuration));
                _logger.LogWarning(
                    "Account locked for {Email} after {Count} failed attempts",
                    Sanitize(request.Email), user.FailedLoginCount);
            }
            else
            {
                _logger.LogWarning(
                    "Failed login attempt #{Count} for {Email}",
                    user.FailedLoginCount, Sanitize(request.Email));
            }

            await _users.SaveChangesAsync(ct);
            throw new InvalidCredentialsException();
        }

        user.ResetFailedLogin();
        await _users.SaveChangesAsync(ct);

        _logger.LogInformation("User logged in: {Email}", Sanitize(request.Email));
        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var existingToken = await _refreshTokens.FindActiveByValueAsync(request.RefreshToken, ct);

        if (existingToken == null)
            throw new InvalidRefreshTokenException();

        var user = await _users.FindByIdAsync(existingToken.UserId, ct);

        if (user == null)
            throw new InvalidRefreshTokenException();

        existingToken.Revoke();
        await _refreshTokens.SaveChangesAsync(ct);

        _logger.LogInformation("Refresh token rotated for user {UserId}", user.Id);
        return await IssueTokensAsync(user, ct);
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, CancellationToken ct)
    {
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshTokenValue = _tokenService.GenerateRefreshToken();
        var expiresAt = DateTimeOffset.UtcNow.Add(_tokenService.RefreshTokenLifetime);

        var refreshToken = RefreshToken.Create(user.Id, refreshTokenValue, expiresAt);
        await _refreshTokens.AddAsync(refreshToken, ct);
        await _refreshTokens.SaveChangesAsync(ct);

        return new AuthResponse(accessToken, refreshTokenValue);
    }

    private static string Sanitize(string input) =>
        input.Replace("\r", "").Replace("\n", "").Replace("\t", "");
}
