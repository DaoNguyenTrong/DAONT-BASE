using FeedbackHub.Domain.Exceptions;

namespace FeedbackHub.Domain.Entities;

public record AccountParams(
    string Name,
    string Username,
    string Email,
    string? Phone = null,
    string? Position = null,
    string? Address = null,
    bool Status = true);

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
        Status = p.Status;
        Username = p.Username.Trim();
        Email = p.Email.Trim();
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
