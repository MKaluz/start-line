using StartLine.Application.Events;
using StartLine.Application.Registrations;
using StartLine.Domain.Events;

namespace StartLine.Infrastructure.Events;

public class EventService : IEventService
{
    private readonly IEventRepository _events;
    private readonly IRegistrationRepository _registrations;

    public EventService(IEventRepository events, IRegistrationRepository registrations)
    {
        _events = events;
        _registrations = registrations;
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

        return await MapDetailAsync(@event, ct);
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
            organizerId,
            request.MinAge,
            request.MaxAge,
            request.AllowedGender);

        await _events.AddRaceAsync(race, ct);
        await _events.SaveChangesAsync(ct);

        // Newly created race has 0 active registrations
        return MapRace(race, activeCount: 0);
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

        return await MapDetailAsync(@event, ct);
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

        return await MapDetailAsync(@event, ct);
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

    private async Task<EventDetailResponse> MapDetailAsync(Event @event, CancellationToken ct)
    {
        var raceIds = @event.Races.Select(r => r.Id).ToList();
        var activeCounts = await _registrations.GetActiveCountsForRacesAsync(raceIds, ct);

        return new EventDetailResponse(
            @event.Id,
            @event.Name,
            @event.Date,
            @event.Location,
            @event.Description,
            @event.Races.Select(r => MapRace(r, activeCounts.GetValueOrDefault(r.Id, 0))).ToList());
    }

    private static RaceResponse MapRace(Race race, int activeCount) =>
        new(
            race.Id,
            race.Name,
            race.Capacity,
            race.Capacity - activeCount,
            race.BasePrice,
            race.EarlyBirdPrice,
            race.EarlyBirdDeadline,
            race.MinAge,
            race.MaxAge,
            race.AllowedGender?.ToString());
}
