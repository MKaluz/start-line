using StartLine.Application.Events;
using StartLine.Domain.Events;

namespace StartLine.Infrastructure.Events;

public class EventService : IEventService
{
    private readonly IEventRepository _events;

    public EventService(IEventRepository events)
    {
        _events = events;
    }

    public async Task<EventDetailResponse> CreateEventAsync(
        CreateEventRequest request,
        Guid? organizerId,
        CancellationToken ct = default)
    {
        var @event = Event.Create(
            request.Name,
            request.Date,
            request.Location,
            request.Description,
            organizerId);

        await _events.AddAsync(@event, ct);
        await _events.SaveChangesAsync(ct);

        return MapDetail(@event);
    }

    public async Task<RaceResponse> AddRaceAsync(
        Guid eventId,
        AddRaceRequest request,
        Guid? organizerId,
        CancellationToken ct = default)
    {
        var @event = await _events.FindByIdAsync(eventId, ct)
            ?? throw new EventNotFoundException(eventId);

        var race = Race.Create(
            eventId,
            request.Name,
            request.Capacity,
            request.BasePrice,
            request.EarlyBirdPrice,
            request.EarlyBirdDeadline,
            organizerId);

        await _events.AddRaceAsync(race, ct);
        await _events.SaveChangesAsync(ct);

        return MapRace(race);
    }

    public async Task<PagedResult<EventSummaryResponse>> ListEventsAsync(
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var (items, totalCount) = await _events.ListUpcomingAsync(page, pageSize, ct);

        var summaries = items
            .Select(e => new EventSummaryResponse(e.Id, e.Name, e.Date, e.Location, e.Description))
            .ToList();

        return new PagedResult<EventSummaryResponse>(summaries, totalCount, page, pageSize);
    }

    public async Task<EventDetailResponse> GetEventAsync(Guid eventId, CancellationToken ct = default)
    {
        var @event = await _events.FindByIdAsync(eventId, ct)
            ?? throw new EventNotFoundException(eventId);

        if (@event.IsDeleted)
            throw new EventNotFoundException(eventId);

        return MapDetail(@event);
    }

    public async Task<EventDetailResponse> UpdateEventAsync(
        Guid eventId,
        UpdateEventRequest request,
        CancellationToken ct = default)
    {
        var @event = await _events.FindByIdAsync(eventId, ct)
            ?? throw new EventNotFoundException(eventId);

        if (@event.IsDeleted)
            throw new EventNotFoundException(eventId);

        @event.Update(request.Name, request.Date, request.Location, request.Description);
        await _events.SaveChangesAsync(ct);

        return MapDetail(@event);
    }

    public async Task SoftDeleteEventAsync(Guid eventId, Guid deletedBy, CancellationToken ct = default)
    {
        var @event = await _events.FindByIdAsync(eventId, ct)
            ?? throw new EventNotFoundException(eventId);

        if (@event.IsDeleted)
            throw new EventNotFoundException(eventId);

        @event.SoftDelete(deletedBy);
        await _events.SaveChangesAsync(ct);
    }

    // ── Mapping helpers ────────────────────────────────────────────────────────

    private static EventDetailResponse MapDetail(Event @event) =>
        new(
            @event.Id,
            @event.Name,
            @event.Date,
            @event.Location,
            @event.Description,
            @event.Races.Select(MapRace).ToList());

    private static RaceResponse MapRace(Race race) =>
        new(
            race.Id,
            race.Name,
            race.Capacity,
            race.Capacity,   // AvailableCapacity = Capacity until registrations are introduced
            race.BasePrice,
            race.EarlyBirdPrice,
            race.EarlyBirdDeadline);
}
