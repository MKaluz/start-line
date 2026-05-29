using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using StartLine.Application.Registrations;
using StartLine.Domain.Events;
using StartLine.Domain.Outbox;
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
                         r.Status != RegistrationStatus.Cancelled &&
                         r.Status != RegistrationStatus.Expired &&
                         r.Status != RegistrationStatus.Waitlisted,
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
            .Where(r => ids.Contains(r.RaceId) &&
                        r.Status != RegistrationStatus.Cancelled &&
                        r.Status != RegistrationStatus.Expired &&
                        r.Status != RegistrationStatus.Waitlisted)
            .GroupBy(r => r.RaceId)
            .Select(g => new { RaceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RaceId, x => x.Count, ct);
    }

    public async Task ConfirmPaymentAsync(
        Registration registration,
        OutboxMessage outboxMessage,
        CancellationToken ct = default)
    {
        await using var tx = await _context.Database
            .BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        try
        {
            // Registration is already tracked by the change tracker (loaded via FindByIdAsync above).
            // Status change via ConfirmPayment() will be detected automatically.
            await _context.OutboxMessages.AddAsync(outboxMessage, ct);
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<int> ExpireReservationsAsync(CancellationToken ct = default)
    {
        await using var tx = await _context.Database
            .BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var expired = await _context.Registrations
                .Where(r => r.Status == RegistrationStatus.Reserved && r.ReservationExpiresAt < now)
                .ToListAsync(ct);

            if (expired.Count == 0)
            {
                await tx.RollbackAsync(ct);
                return 0;
            }

            var outboxMessages = new List<OutboxMessage>(expired.Count);
            foreach (var reg in expired)
            {
                reg.Expire();
                var payload = JsonSerializer.Serialize(new
                {
                    RegistrationId = reg.Id,
                    reg.RaceId,
                    reg.AthleteId,
                    reg.Email
                });
                outboxMessages.Add(OutboxMessage.Create("ReservationExpiredEmail", payload));
            }

            // For each race that had expiries, promote waitlist entries (one per freed slot)
            var freedSlotsByRace = expired
                .GroupBy(r => r.RaceId)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var (raceId, freedCount) in freedSlotsByRace)
            {
                var waitlistEntries = await _context.Registrations
                    .Where(r => r.RaceId == raceId && r.Status == RegistrationStatus.Waitlisted)
                    .OrderBy(r => r.QueuePosition)
                    .Take(freedCount)
                    .ToListAsync(ct);

                foreach (var entry in waitlistEntries)
                {
                    entry.PromoteFromWaitlist();
                    var promotionPayload = JsonSerializer.Serialize(new
                    {
                        RegistrationId = entry.Id,
                        entry.RaceId,
                        entry.AthleteId,
                        entry.Email
                    });
                    outboxMessages.Add(OutboxMessage.Create("WaitlistPromotedEmail", promotionPayload));
                }
            }

            await _context.OutboxMessages.AddRangeAsync(outboxMessages, ct);
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return expired.Count;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task AddToWaitlistAsync(Registration registration, CancellationToken ct = default)
    {
        await using var tx = await _context.Database
            .BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        try
        {
            // Acquire an exclusive row-level lock on the Race row to serialize concurrent waitlist insertions.
            await _context.Database.ExecuteSqlAsync(
                $"UPDATE \"Races\" SET \"Name\" = \"Name\" WHERE \"Id\" = {registration.RaceId}",
                ct);

            var maxPosition = await _context.Registrations
                .Where(r => r.RaceId == registration.RaceId && r.Status == RegistrationStatus.Waitlisted)
                .MaxAsync(r => (int?)r.QueuePosition, ct) ?? 0;

            registration.AssignQueuePosition(maxPosition + 1);

            await _context.Registrations.AddAsync(registration, ct);
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task CancelRegistrationAsync(Registration registration, CancellationToken ct = default)
    {
        await using var tx = await _context.Database
            .BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        try
        {
            var freesCapacity = registration.Status == RegistrationStatus.Reserved ||
                                registration.Status == RegistrationStatus.Paid;

            registration.Cancel();

            var outboxMessages = new List<OutboxMessage>();

            if (freesCapacity)
            {
                // Promote the next waitlist entry if one exists
                var nextEntry = await _context.Registrations
                    .Where(r => r.RaceId == registration.RaceId && r.Status == RegistrationStatus.Waitlisted)
                    .OrderBy(r => r.QueuePosition)
                    .FirstOrDefaultAsync(ct);

                if (nextEntry is not null)
                {
                    nextEntry.PromoteFromWaitlist();
                    var promotionPayload = JsonSerializer.Serialize(new
                    {
                        RegistrationId = nextEntry.Id,
                        nextEntry.RaceId,
                        nextEntry.AthleteId,
                        nextEntry.Email
                    });
                    outboxMessages.Add(OutboxMessage.Create("WaitlistPromotedEmail", promotionPayload));
                }
            }

            await _context.OutboxMessages.AddRangeAsync(outboxMessages, ct);
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
