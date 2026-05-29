using StartLine.Domain.Events;
using StartLine.Domain.Outbox;
using StartLine.Domain.Registrations;

namespace StartLine.Application.Registrations;

public interface IRegistrationRepository
{
    Task<(Race Race, DateOnly EventDate)?> FindRaceWithEventDateAsync(Guid raceId, CancellationToken ct = default);
    Task<Registration?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> TryReserveAsync(Registration registration, int raceCapacity, CancellationToken ct = default);
    Task<Dictionary<Guid, int>> GetActiveCountsForRacesAsync(IEnumerable<Guid> raceIds, CancellationToken ct = default);

    /// <summary>Atomically updates the registration status to <see cref="Domain.Registrations.RegistrationStatus.Paid"/>
    /// and persists the outbox message in the same database transaction.</summary>
    Task ConfirmPaymentAsync(Registration registration, OutboxMessage outboxMessage, CancellationToken ct = default);

    /// <summary>Finds all <see cref="Domain.Registrations.RegistrationStatus.Reserved"/> registrations whose
    /// <see cref="Registration.ReservationExpiresAt"/> has passed, transitions them to
    /// <see cref="Domain.Registrations.RegistrationStatus.Expired"/>, and writes one
    /// <see cref="OutboxMessage"/> of type <c>ReservationExpiredEmail</c> per expiry — all in a single
    /// atomic database transaction. Returns the number of registrations expired.</summary>
    Task<int> ExpireReservationsAsync(CancellationToken ct = default);

    /// <summary>Assigns the next available queue position to <paramref name="registration"/> and persists it
    /// as a <see cref="Domain.Registrations.RegistrationStatus.Waitlisted"/> entry in an atomic transaction.</summary>
    Task AddToWaitlistAsync(Registration registration, CancellationToken ct = default);

    /// <summary>Transitions <paramref name="registration"/> to
    /// <see cref="Domain.Registrations.RegistrationStatus.Cancelled"/>.
    /// If the registration was <see cref="Domain.Registrations.RegistrationStatus.Reserved"/> or
    /// <see cref="Domain.Registrations.RegistrationStatus.Paid"/>, the freed slot is used to promote
    /// the <see cref="Domain.Registrations.RegistrationStatus.Waitlisted"/> entry with the lowest queue
    /// position — writing a <c>WaitlistPromotedEmail</c> outbox message — all in the same transaction.</summary>
    Task CancelRegistrationAsync(Registration registration, CancellationToken ct = default);
}
