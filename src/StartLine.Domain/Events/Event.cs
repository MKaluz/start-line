using StartLine.Domain.Common;

namespace StartLine.Domain.Events;

public class Event : Entity
{
    public string Name { get; private set; } = default!;
    public DateOnly Date { get; private set; }
    public string Location { get; private set; } = default!;
    public string? Description { get; private set; }
    public Guid? OrganizerId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // Soft delete
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }

    private readonly List<Race> _races = new();
    public IReadOnlyList<Race> Races => _races.AsReadOnly();

    private Event() { }

    public static Event Create(string name, DateOnly date, string location, string? description, Guid? organizerId)
    {
        return new Event
        {
            Name = name,
            Date = date,
            Location = location,
            Description = description,
            OrganizerId = organizerId,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Update(string name, DateOnly date, string location, string? description)
    {
        Name = name;
        Date = date;
        Location = location;
        Description = description;
    }

    public void SoftDelete(Guid deletedBy)
    {
        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
        DeletedBy = deletedBy;
    }

    /// <summary>
    /// Calculates the age (in whole years) of a person born on <paramref name="birthDate"/>
    /// as of the event date. Age category validation should use this value.
    /// </summary>
    public int AgeOnEventDate(DateOnly birthDate)
    {
        var age = Date.Year - birthDate.Year;
        if (birthDate > Date.AddYears(-age))
            age--;
        return age;
    }
}
