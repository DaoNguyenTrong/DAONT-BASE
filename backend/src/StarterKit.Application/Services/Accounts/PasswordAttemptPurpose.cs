namespace StarterKit.Application.Services.Accounts;

/// <summary>
/// Which password check an attempt counts against. Each has its own counter and lock: someone guessing the
/// current password through a stolen session must not lock the owner out of signing in, and failed sign-ins
/// must not block the owner from changing a password they suspect has leaked.
/// </summary>
public enum PasswordAttemptPurpose
{
    Login,
    PasswordChange
}
