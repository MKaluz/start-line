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
}
