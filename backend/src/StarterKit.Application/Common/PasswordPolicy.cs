namespace StarterKit.Application.Common;

/// <summary>
/// Password rules, applied where a password is <b>set</b> (register, a change's new password), never where one is only
/// <b>compared</b> (login, a change's current password): one set under an older, looser policy must still sign in,
/// or tightening the policy locks those accounts out. UI copy: <c>frontend/src/lib/password-policy.ts</c>; change both.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;

    public const int MaxLength = 100;
}
