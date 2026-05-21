using Microsoft.EntityFrameworkCore;
using StartLine.Application.Users;
using StartLine.Domain.Users;

namespace StartLine.Infrastructure.Persistence.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _context;

    public RefreshTokenRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<RefreshToken?> FindActiveByValueAsync(string tokenValue, CancellationToken ct = default) =>
        _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == tokenValue && rt.RevokedAt == null && rt.ExpiresAt > DateTimeOffset.UtcNow, ct);

    public async Task AddAsync(RefreshToken token, CancellationToken ct = default) =>
        await _context.RefreshTokens.AddAsync(token, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        _context.SaveChangesAsync(ct);
}
