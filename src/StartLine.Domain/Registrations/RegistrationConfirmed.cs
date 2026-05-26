using StartLine.Domain.Common;

namespace StartLine.Domain.Registrations;

/// <summary>Raised when an Athlete successfully pays for a reservation.</summary>
public record RegistrationConfirmed(
    Guid RegistrationId,
    Guid RaceId,
    Guid AthleteId,
    string Email) : IDomainEvent;
