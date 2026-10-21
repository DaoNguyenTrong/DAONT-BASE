using StarterKit.Application.Common.Interfaces;
using StarterKit.Infrastructure.Services.Auth;

namespace StarterKit.API.Tests.TestSupport;

/// <summary>
/// The real <c>PasswordHasher</c> at BCrypt cost 4 (the minimum) instead of 12 (~0.3s per hash/verify): API.Tests check
/// the HTTP flow, not hash strength; Infrastructure.Tests' <c>PasswordHasherTests</c> pins the production cost.
/// <c>Verify</c> reads the cost from the hash itself, so a hash seeded at any cost still verifies (12 is just slow).
/// </summary>
public static class FastPasswordHasher
{
    public const int WorkFactor = 4;

    public static IPasswordHasher Create() => new PasswordHasher(WorkFactor);
}
