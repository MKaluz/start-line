using Microsoft.EntityFrameworkCore;
using StartLine.Application.Events;
using StartLine.Domain.Events;
using StartLine.Infrastructure.Persistence;

namespace StartLine.Infrastructure.Events;

public class EventRepository : IEventRepository
{
    private readonly AppDbContext _context;

    public EventRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Event?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.Events
            .Include(e => e.Races)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<(IReadOnlyList<Event> Items, int TotalCount)> ListUpcomingAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var query = _context.Events
            .Where(e => !e.IsDeleted && e.Date >= today)
            .OrderBy(e => e.Date);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(Event @event, CancellationToken ct = default) =>
        await _context.Events.AddAsync(@event, ct);

    public async Task AddRaceAsync(Race race, CancellationToken ct = default) =>
        await _context.Races.AddAsync(race, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        _context.SaveChangesAsync(ct);
}
