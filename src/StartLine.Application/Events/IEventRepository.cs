using StartLine.Domain.Events;

namespace StartLine.Application.Events;

public interface IEventRepository
{
    Task<Event?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<Event> Items, int TotalCount)> ListUpcomingAsync(int page, int pageSize, CancellationToken ct = default);
    Task AddAsync(Event @event, CancellationToken ct = default);
    Task AddRaceAsync(Race race, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
