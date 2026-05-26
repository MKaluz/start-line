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
}
