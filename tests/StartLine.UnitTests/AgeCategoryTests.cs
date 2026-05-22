using StartLine.Domain.Events;

namespace StartLine.UnitTests;

public class AgeCategoryTests
{
    // Event on 2024-06-15
    private static readonly Event SummerEvent =
        Event.Create("Summer Race", new DateOnly(2024, 6, 15), "Prague", null, null);

    [Fact]
    public void AgeOnEventDate_BirthDayBeforeEvent_CorrectAge()
    {
        // Born 1990-01-01 → turns 34 before event date
        var age = SummerEvent.AgeOnEventDate(new DateOnly(1990, 1, 1));
        Assert.Equal(34, age);
    }

    [Fact]
    public void AgeOnEventDate_BirthdayOnEventDate_AlreadyTurnedAge()
    {
        // Born 1990-06-15 → birthday IS the event date → turns 34 on that day
        var age = SummerEvent.AgeOnEventDate(new DateOnly(1990, 6, 15));
        Assert.Equal(34, age);
    }

    [Fact]
    public void AgeOnEventDate_BirthdayAfterEventDate_NotYetTurnedAge()
    {
        // Born 1990-06-16 → birthday is the day AFTER the event → still 33
        var age = SummerEvent.AgeOnEventDate(new DateOnly(1990, 6, 16));
        Assert.Equal(33, age);
    }

    [Fact]
    public void AgeOnEventDate_BirthdayLastDayOfYear_CorrectAge()
    {
        // Born 1990-12-31 → event is in June → still 33 on event date
        var age = SummerEvent.AgeOnEventDate(new DateOnly(1990, 12, 31));
        Assert.Equal(33, age);
    }

    [Fact]
    public void AgeOnEventDate_LeapYearBirthday_CorrectAge()
    {
        // Born 1992-02-29 (leap) → event on 2024-06-15 → turns 32 before event
        var age = SummerEvent.AgeOnEventDate(new DateOnly(1992, 2, 29));
        Assert.Equal(32, age);
    }

    [Fact]
    public void AgeOnEventDate_UsesEventDateNotToday()
    {
        // Sanity check: age calculated as of 2024-06-15, not today
        var eventDate = new DateOnly(2024, 6, 15);
        var @event = Event.Create("Test", eventDate, "City", null, null);
        var birthDate = new DateOnly(2000, 6, 15);

        var age = @event.AgeOnEventDate(birthDate);
        Assert.Equal(24, age);
    }
}
