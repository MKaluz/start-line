namespace StartLine.Application.Registrations;

public class RaceNotFoundException : Exception
{
    public RaceNotFoundException(Guid id)
        : base($"Race '{id}' was not found.") { }
}

public class CapacityExceededException : Exception
{
    public CapacityExceededException(Guid raceId)
        : base($"Race '{raceId}' has no available capacity.") { }
}

public class AgeValidationException : Exception
{
    public AgeValidationException(string message) : base(message) { }
}

public class GenderValidationException : Exception
{
    public GenderValidationException(string message) : base(message) { }
}

public class RegistrationNotFoundException : Exception
{
    public RegistrationNotFoundException(Guid id)
        : base($"Registration '{id}' was not found.") { }
}
