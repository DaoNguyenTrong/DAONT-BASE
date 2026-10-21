using StarterKit.Domain.Exceptions;

namespace StarterKit.Domain.Entities;

public record AccountParams(
    string Name,
    string Username,
    string Email,
    string? Phone = null,
    string? Position = null,
    string? Address = null);

public sealed class Account : BaseEntity<Guid>
{
    private Account()
    {
    }

    public static Account Create(AccountParams p)
    {
        Account account = new()
        {
            Id = IdGenerator.NewUuidV7()
        };
        account.Update(p);
        return account;
    }

    /// <summary>
    /// The one canonical form of a username, used for every write AND every lookup — otherwise the
    /// case-sensitive unique index lets "Alice" and "alice" coexist and login depends on casing.
    /// Invariant culture: a culture-sensitive lower-case would turn "I" into "ı" under tr-TR.
    /// </summary>
    public static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();

    /// <summary>Same as <see cref="NormalizeUsername"/>, for email addresses.</summary>
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public string Name { get; private set; } = string.Empty;

    public string? Phone { get; private set; }

    public string? Position { get; private set; }

    public string? Address { get; private set; }

    public bool Status { get; private set; } = true;

    /// <summary>Storage path of the profile picture, or <c>null</c> when none is set.</summary>
    public string? AvatarPath { get; private set; }

    public void SetAvatar(string? path) =>
        AvatarPath = string.IsNullOrWhiteSpace(path) ? null : path.Trim();

    public string Username { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string? PasswordHash { get; private set; }

    public bool EmailConfirmed { get; private set; }

    public void ConfirmEmail() => EmailConfirmed = true;

    /// <summary>Wrong-password attempts inside the current lockout window.</summary>
    public int FailedLoginCount { get; private set; }

    /// <summary>Start of the window <see cref="FailedLoginCount"/> is counted within.</summary>
    public DateTime? FailedLoginWindowStartedAt { get; private set; }

    /// <summary>End of the temporary sign-in lock, or <c>null</c> when sign-in is not locked.</summary>
    public DateTime? LockedUntil { get; private set; }

    /// <summary>Wrong current-password attempts on a password change — counted apart from sign-in.</summary>
    public int FailedPasswordChangeCount { get; private set; }

    /// <summary>Start of the window <see cref="FailedPasswordChangeCount"/> is counted within.</summary>
    public DateTime? PasswordChangeWindowStartedAt { get; private set; }

    /// <summary>End of the temporary password-change lock, or <c>null</c> when it is not locked.</summary>
    public DateTime? PasswordChangeLockedUntil { get; private set; }

    /// <summary>
    /// The only way to change <see cref="Status"/> — deliberately not part of <see cref="AccountParams"/>,
    /// so an ordinary profile edit can never reactivate (or deactivate) an account by accident.
    /// </summary>
    public void Deactivate() => Status = false;

    public void Activate() => Status = true;

    public void Update(AccountParams p)
    {
        if (string.IsNullOrWhiteSpace(p.Name))
        {
            throw new DomainException(DomainMessages.AccountNameRequired);
        }

        if (string.IsNullOrWhiteSpace(p.Username))
        {
            throw new DomainException(DomainMessages.AccountUsernameRequired);
        }

        if (string.IsNullOrWhiteSpace(p.Email))
        {
            throw new DomainException(DomainMessages.AccountEmailRequired);
        }

        Name = p.Name.Trim();
        Phone = string.IsNullOrWhiteSpace(p.Phone) ? null : p.Phone.Trim();
        Position = string.IsNullOrWhiteSpace(p.Position) ? null : p.Position.Trim();
        Address = string.IsNullOrWhiteSpace(p.Address) ? null : p.Address.Trim();
        Username = NormalizeUsername(p.Username);
        Email = NormalizeEmail(p.Email);
    }

    public void SetPasswordHash(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            throw new DomainException(DomainMessages.PasswordHashRequired);
        }

        PasswordHash = hash.Trim();
    }
}
