namespace StartLine.Application.Auth;

public class DuplicateEmailException : Exception
{
    public DuplicateEmailException(string email)
        : base($"An account with email '{email}' already exists.") { }
}

public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException()
        : base("Invalid email or password.") { }
}

public class AccountLockedException : Exception
{
    public AccountLockedException()
        : base("Account is temporarily locked due to multiple failed login attempts.") { }
}

public class InvalidRefreshTokenException : Exception
{
    public InvalidRefreshTokenException()
        : base("The refresh token is invalid or has expired.") { }
}
