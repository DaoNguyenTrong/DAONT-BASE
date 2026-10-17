using StarterKit.Application.Common.Interfaces;

namespace StarterKit.Infrastructure.Services.Auth;

public sealed class PasswordHasher : IPasswordHasher
{
    private const int ProductionWorkFactor = 12;

    private readonly int workFactor;

    public PasswordHasher() : this(ProductionWorkFactor)
    {
    }

    /// <summary>Tests only: work factor 12 costs ~0.3s per hash.</summary>
    internal PasswordHasher(int workFactor)
    {
        this.workFactor = workFactor;
    }

    public string Hash(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor);
    }

    public bool Verify(string password, string passwordHash)
    {
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }
}
