using StartLine.Domain.Common;

namespace StartLine.Domain.Users;

public class User : Entity
{
    public string Email { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public Role Role { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public int FailedLoginCount { get; private set; }
    public DateTimeOffset? LockoutEnd { get; private set; }

    private User() { }

    public static User Create(string email, string passwordHash, Role role)
    {
        return new User
        {
            Email = email.ToLowerInvariant().Trim(),
            PasswordHash = passwordHash,
            Role = role,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void RecordFailedLogin()
    {
        FailedLoginCount++;
    }

    public void ResetFailedLogin()
    {
        FailedLoginCount = 0;
        LockoutEnd = null;
    }

    public void LockUntil(DateTimeOffset until)
    {
        LockoutEnd = until;
    }

    public bool IsLockedOut() =>
        LockoutEnd.HasValue && LockoutEnd.Value > DateTimeOffset.UtcNow;
}

