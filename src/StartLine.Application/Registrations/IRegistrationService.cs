using StartLine.Domain.Users;

namespace StartLine.Application.Registrations;

// ── Requests ──────────────────────────────────────────────────────────────────

public record CreateRegistrationRequest(
    Guid RaceId,
    string FirstName,
    string LastName,
    string Email,
    DateOnly DateOfBirth,
    Gender Gender,
    string? Club,
    string? Phone);

// ── Responses ─────────────────────────────────────────────────────────────────

public record RegistrationResponse(
    Guid Id,
    Guid RaceId,
    Guid AthleteId,
    string Status,
    DateTimeOffset ReservationExpiresAt,
    string FirstName,
    string LastName,
    string Email,
    DateOnly DateOfBirth,
    string Gender,
    string? Club,
    string? Phone,
    DateTimeOffset CreatedAt);

// ── Service interface ─────────────────────────────────────────────────────────

public interface IRegistrationService
{
    Task<RegistrationResponse> RegisterAsync(
        CreateRegistrationRequest request,
        Guid athleteId,
        CancellationToken ct = default);

    Task<RegistrationResponse> GetRegistrationAsync(
        Guid registrationId,
        Guid athleteId,
        CancellationToken ct = default);

    Task<RegistrationResponse> PayRegistrationAsync(
        Guid registrationId,
        Guid athleteId,
        CancellationToken ct = default);
}
