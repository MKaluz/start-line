using StartLine.Domain.Common;

namespace StartLine.Domain.Events;

public class Race : Entity
{
    public Guid EventId { get; private set; }
    public string Name { get; private set; } = default!;
    public int Capacity { get; private set; }
    public decimal BasePrice { get; private set; }
    public decimal? EarlyBirdPrice { get; private set; }
    public DateOnly? EarlyBirdDeadline { get; private set; }
    public Guid? OrganizerId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Race() { }

    public static Race Create(
        Guid eventId,
        string name,
        int capacity,
        decimal basePrice,
        decimal? earlyBirdPrice,
        DateOnly? earlyBirdDeadline,
        Guid? organizerId)
    {
        return new Race
        {
            EventId = eventId,
            Name = name,
            Capacity = capacity,
            BasePrice = basePrice,
            EarlyBirdPrice = earlyBirdPrice,
            EarlyBirdDeadline = earlyBirdDeadline,
            OrganizerId = organizerId,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
