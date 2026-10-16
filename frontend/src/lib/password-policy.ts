/**
 * UI copy of the backend's `PasswordPolicy` (`backend/src/StarterKit.Application/Common/PasswordPolicy.cs`) —
 * change both together. Applies only where a password is set (register, the new password of a change),
 * never to login or the current password: those may predate the policy.
 */
export const PASSWORD_MIN_LENGTH = 8
