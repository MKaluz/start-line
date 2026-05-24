using StartLine.Domain.Users;

namespace StartLine.Application.Events;

// ── Requests ──────────────────────────────────────────────────────────────────

public record CreateEventRequest(string Name, DateOnly Date, string Location, string? Description);

public record AddRaceRequest(
    string Name,
    int Capacity,
    decimal BasePrice,
    decimal? EarlyBirdPrice,
    DateOnly? EarlyBirdDeadline,
    int? MinAge = null,
    int? MaxAge = null,
    Gender? AllowedGender = null);

public record UpdateEventRequest(string Name, DateOnly Date, string Location, string? Description);

// ── Responses ─────────────────────────────────────────────────────────────────

public record RaceResponse(
    Guid Id,
    string Name,
    int Capacity,
    int AvailableCapacity,
    decimal BasePrice,
    decimal? EarlyBirdPrice,
    DateOnly? EarlyBirdDeadline,
    int? MinAge,
    int? MaxAge,
    string? AllowedGender);

public record EventSummaryResponse(
    Guid Id,
    string Name,
    DateOnly Date,
    string Location,
    string? Description);

public record EventDetailResponse(
    Guid Id,
    string Name,
    DateOnly Date,
    string Location,
    string? Description,
    IReadOnlyList<RaceResponse> Races);

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);

// ── Service interface ─────────────────────────────────────────────────────────

public interface IEventService
{
    Task<EventDetailResponse> CreateEventAsync(CreateEventRequest request, Guid? organizerId, CancellationToken ct = default);
    Task<RaceResponse> AddRaceAsync(Guid eventId, AddRaceRequest request, Guid? organizerId, CancellationToken ct = default);
    Task<PagedResult<EventSummaryResponse>> ListEventsAsync(int page, int pageSize, CancellationToken ct = default);
    Task<EventDetailResponse> GetEventAsync(Guid eventId, CancellationToken ct = default);
    Task<EventDetailResponse> UpdateEventAsync(Guid eventId, UpdateEventRequest request, CancellationToken ct = default);
    Task SoftDeleteEventAsync(Guid eventId, Guid deletedBy, CancellationToken ct = default);
}
