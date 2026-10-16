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

    public string Username { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string? PasswordHash { get; private set; }

    public bool EmailConfirmed { get; private set; }

    public void ConfirmEmail() => EmailConfirmed = true;

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
