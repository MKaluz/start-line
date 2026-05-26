using StartLine.Application.Registrations;
using StartLine.Domain.Registrations;
using StartLine.Domain.Users;

namespace StartLine.Infrastructure.Registrations;

public class RegistrationService : IRegistrationService
{
    private readonly IRegistrationRepository _registrations;

    public RegistrationService(IRegistrationRepository registrations)
    {
        _registrations = registrations;
    }

    public async Task<RegistrationResponse> RegisterAsync(
        CreateRegistrationRequest request,
        Guid athleteId,
        CancellationToken ct = default)
    {
        var raceWithDate = await _registrations.FindRaceWithEventDateAsync(request.RaceId, ct)
            ?? throw new RaceNotFoundException(request.RaceId);

        var (race, eventDate) = raceWithDate;

        // Age validation
        if (race.MinAge.HasValue || race.MaxAge.HasValue)
        {
            var age = CalculateAge(request.DateOfBirth, eventDate);

            if (race.MinAge.HasValue && age < race.MinAge.Value)
                throw new AgeValidationException(
                    $"Athlete age {age} is below the minimum required age of {race.MinAge.Value} for this race.");

            if (race.MaxAge.HasValue && age > race.MaxAge.Value)
                throw new AgeValidationException(
                    $"Athlete age {age} exceeds the maximum allowed age of {race.MaxAge.Value} for this race.");
        }

        // Gender validation
        if (race.AllowedGender.HasValue && race.AllowedGender.Value != request.Gender)
            throw new GenderValidationException(
                $"This race is restricted to {race.AllowedGender.Value} athletes.");

        var registration = Registration.Create(
            request.RaceId,
            athleteId,
            request.FirstName,
            request.LastName,
            request.Email,
            request.DateOfBirth,
            request.Gender,
            request.Club,
            request.Phone);

        var reserved = await _registrations.TryReserveAsync(registration, race.Capacity, ct);

        if (!reserved)
            throw new CapacityExceededException(request.RaceId);

        return Map(registration);
    }

    public async Task<RegistrationResponse> GetRegistrationAsync(
        Guid registrationId,
        Guid athleteId,
        CancellationToken ct = default)
    {
        var registration = await _registrations.FindByIdAsync(registrationId, ct)
            ?? throw new RegistrationNotFoundException(registrationId);

        // Only the owning athlete can view their registration
        if (registration.AthleteId != athleteId)
            throw new RegistrationNotFoundException(registrationId);

        return Map(registration);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int CalculateAge(DateOnly birthDate, DateOnly asOf)
    {
        var age = asOf.Year - birthDate.Year;
        if (birthDate > asOf.AddYears(-age))
            age--;
        return age;
    }

    private static RegistrationResponse Map(Registration r) =>
        new(
            r.Id,
            r.RaceId,
            r.AthleteId,
            r.Status.ToString(),
            r.ReservationExpiresAt,
            r.FirstName,
            r.LastName,
            r.Email,
            r.DateOfBirth,
            r.Gender.ToString(),
            r.Club,
            r.Phone,
            r.CreatedAt);
}
