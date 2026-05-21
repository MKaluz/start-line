using StartLine.Domain.Users;

namespace StartLine.Application.Users;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> FindActiveByValueAsync(string tokenValue, CancellationToken ct = default);
    Task AddAsync(RefreshToken token, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
