namespace StartLine.Application.Events;

public class EventNotFoundException : Exception
{
    public EventNotFoundException(Guid id)
        : base($"Event '{id}' was not found.") { }
}
