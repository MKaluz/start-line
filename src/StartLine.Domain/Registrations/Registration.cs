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

    private Registration() { }

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
