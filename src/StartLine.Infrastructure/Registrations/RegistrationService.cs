using System.Text.Json;
using StartLine.Application.Payments;
using StartLine.Application.Registrations;
using StartLine.Domain.Outbox;
using StartLine.Domain.Registrations;
using StartLine.Domain.Users;

namespace StartLine.Infrastructure.Registrations;

public class RegistrationService : IRegistrationService
{
    private readonly IRegistrationRepository _registrations;
    private readonly IPaymentProvider _paymentProvider;

    public RegistrationService(IRegistrationRepository registrations, IPaymentProvider paymentProvider)
    {
        _registrations = registrations;
        _paymentProvider = paymentProvider;
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
        {
            // Race is at full capacity — add to waitlist instead
            var waitlistEntry = Registration.CreateWaitlistEntry(
                request.RaceId,
                athleteId,
                request.FirstName,
                request.LastName,
                request.Email,
                request.DateOfBirth,
                request.Gender,
                request.Club,
                request.Phone);

            await _registrations.AddToWaitlistAsync(waitlistEntry, ct);
            return Map(waitlistEntry);
        }

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

    public async Task<RegistrationResponse> PayRegistrationAsync(
        Guid registrationId,
        Guid athleteId,
        CancellationToken ct = default)
    {
        var registration = await _registrations.FindByIdAsync(registrationId, ct)
            ?? throw new RegistrationNotFoundException(registrationId);

        // Only the owning athlete can pay for their registration
        if (registration.AthleteId != athleteId)
            throw new RegistrationNotFoundException(registrationId);

        if (registration.Status != RegistrationStatus.Reserved)
            throw new RegistrationInvalidStatusException(registrationId, registration.Status.ToString());

        if (registration.ReservationExpiresAt < DateTimeOffset.UtcNow)
            throw new ReservationExpiredException(registrationId);

        // Process payment — mock always succeeds
        await _paymentProvider.ProcessPaymentAsync(registrationId, ct);

        // Transition domain state and emit RegistrationConfirmed domain event
        registration.ConfirmPayment();

        // Build outbox message payload from the domain event
        var domainEvent = (RegistrationConfirmed)registration.DomainEvents[0];
        var payload = JsonSerializer.Serialize(new
        {
            domainEvent.RegistrationId,
            domainEvent.RaceId,
            domainEvent.AthleteId,
            domainEvent.Email
        });
        var outboxMessage = OutboxMessage.Create("PaymentConfirmedEmail", payload);

        // Persist status update + outbox message atomically
        await _registrations.ConfirmPaymentAsync(registration, outboxMessage, ct);

        registration.ClearDomainEvents();

        return Map(registration);
    }

    public async Task<RegistrationResponse> CancelRegistrationAsync(
        Guid registrationId,
        Guid athleteId,
        CancellationToken ct = default)
    {
        var registration = await _registrations.FindByIdAsync(registrationId, ct)
            ?? throw new RegistrationNotFoundException(registrationId);

        // Only the owning athlete can cancel their registration
        if (registration.AthleteId != athleteId)
            throw new RegistrationNotFoundException(registrationId);

        if (registration.Status == RegistrationStatus.Cancelled ||
            registration.Status == RegistrationStatus.Expired)
            throw new RegistrationCannotBeCancelledException(registrationId, registration.Status.ToString());

        await _registrations.CancelRegistrationAsync(registration, ct);

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
            r.QueuePosition,
            r.FirstName,
            r.LastName,
            r.Email,
            r.DateOfBirth,
            r.Gender.ToString(),
            r.Club,
            r.Phone,
            r.CreatedAt);
}
