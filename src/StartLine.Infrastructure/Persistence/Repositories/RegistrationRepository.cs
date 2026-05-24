using Microsoft.EntityFrameworkCore;
using StartLine.Application.Registrations;
using StartLine.Domain.Events;
using StartLine.Domain.Registrations;
using StartLine.Infrastructure.Persistence;

namespace StartLine.Infrastructure.Persistence.Repositories;

public class RegistrationRepository : IRegistrationRepository
{
    private readonly AppDbContext _context;

    public RegistrationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(Race Race, DateOnly EventDate)?> FindRaceWithEventDateAsync(
        Guid raceId,
        CancellationToken ct = default)
    {
        var result = await _context.Races
            .Join(
                _context.Events,
                r => r.EventId,
                e => e.Id,
                (r, e) => new { Race = r, EventDate = e.Date })
            .FirstOrDefaultAsync(x => x.Race.Id == raceId, ct);

        return result is null ? null : (result.Race, result.EventDate);
    }

    public Task<Registration?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.Registrations.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<bool> TryReserveAsync(
        Registration registration,
        int raceCapacity,
        CancellationToken ct = default)
    {
        await using var tx = await _context.Database
            .BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        try
        {
            // Acquire an exclusive row-level lock on the Race row so concurrent
            // reservations for the same race are serialized at the database level.
            var locked = await _context.Database.ExecuteSqlAsync(
                $"UPDATE \"Races\" SET \"Name\" = \"Name\" WHERE \"Id\" = {registration.RaceId}",
                ct);

            if (locked == 0)
            {
                // Race was deleted between validation and reservation
                await tx.RollbackAsync(ct);
                return false;
            }

            var activeCount = await _context.Registrations
                .CountAsync(
                    r => r.RaceId == registration.RaceId &&
                         r.Status != RegistrationStatus.Cancelled,
                    ct);

            if (activeCount >= raceCapacity)
            {
                await tx.RollbackAsync(ct);
                return false;
            }

            await _context.Registrations.AddAsync(registration, ct);
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return true;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<Dictionary<Guid, int>> GetActiveCountsForRacesAsync(
        IEnumerable<Guid> raceIds,
        CancellationToken ct = default)
    {
        var ids = raceIds.ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, int>();

        return await _context.Registrations
            .Where(r => ids.Contains(r.RaceId) && r.Status != RegistrationStatus.Cancelled)
            .GroupBy(r => r.RaceId)
            .Select(g => new { RaceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RaceId, x => x.Count, ct);
    }
}
