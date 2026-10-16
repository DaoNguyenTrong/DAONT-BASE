namespace StarterKit.Application.Common;

/// <summary>
/// The password rules, in one place. They apply wherever a password is <b>set</b> (register, the new password
/// of a change) — never where an existing one is only <b>compared</b> (login, the current password of a
/// change): a password set under an older, looser policy must still get its owner in, or tightening the policy
/// locks those accounts out. The UI copy lives in <c>frontend/src/lib/password-policy.ts</c> — change both.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;

    public const int MaxLength = 100;
}
