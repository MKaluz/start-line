using StartLine.Domain.Common;
using StartLine.Domain.Users;

namespace StartLine.Domain.Registrations;

public class Registration : Entity
{
    public Guid RaceId { get; private set; }
    public Guid AthleteId { get; private set; }
    public RegistrationStatus Status { get; private set; }
    public DateTimeOffset ReservationExpiresAt { get; private set; }

    // Athlete profile snapshot
    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public DateOnly DateOfBirth { get; private set; }
    public Gender Gender { get; private set; }
    public string? Club { get; private set; }
    public string? Phone { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    private Registration() { }

    /// <summary>Transitions the registration to <see cref="RegistrationStatus.Paid"/> and raises a
    /// <see cref="RegistrationConfirmed"/> domain event. Must only be called on a <see cref="RegistrationStatus.Reserved"/>
    /// registration whose <see cref="ReservationExpiresAt"/> has not passed.</summary>
    public void ConfirmPayment()
    {
        Status = RegistrationStatus.Paid;
        _domainEvents.Add(new RegistrationConfirmed(Id, RaceId, AthleteId, Email));
    }

    /// <summary>Transitions the registration to <see cref="RegistrationStatus.Expired"/>.
    /// Must only be called on a <see cref="RegistrationStatus.Reserved"/> registration whose
    /// <see cref="ReservationExpiresAt"/> has passed.</summary>
    public void Expire()
    {
        Status = RegistrationStatus.Expired;
    }

    public static Registration Create(
        Guid raceId,
        Guid athleteId,
        string firstName,
        string lastName,
        string email,
        DateOnly dateOfBirth,
        Gender gender,
        string? club,
        string? phone)
    {
        return new Registration
        {
            RaceId = raceId,
            AthleteId = athleteId,
            Status = RegistrationStatus.Reserved,
            ReservationExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            DateOfBirth = dateOfBirth,
            Gender = gender,
            Club = club,
            Phone = phone,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
