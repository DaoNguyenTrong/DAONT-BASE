using StarterKit.Application.Common.Interfaces;
using StarterKit.Infrastructure.Services.Auth;

namespace StarterKit.API.Tests.TestSupport;

/// <summary>
/// The real <c>PasswordHasher</c> at BCrypt cost 4 (the minimum) instead of 12. Cost 12 takes ~0.3s per
/// hash/verify — across every register, login, password change and reset that adds up to a large share
/// of API.Tests' run time. Tests at this layer check the HTTP flow, not hash strength; the production
/// cost is pinned by Infrastructure.Tests' <c>PasswordHasherTests</c>. <c>Verify</c> reads the cost
/// from the hash itself, so a hash seeded at any cost still verifies — seeding at 12 is just slow again.
/// </summary>
public static class FastPasswordHasher
{
    public const int WorkFactor = 4;

    public static IPasswordHasher Create() => new PasswordHasher(WorkFactor);
}
