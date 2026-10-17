namespace StarterKit.Domain.Exceptions;

/// <summary>
/// A dependency the request needs (an external provider, a backing service) is down or refusing work right now
/// → 503. The request itself was fine and may succeed on a retry — unlike a 500, which means our own bug.
/// </summary>
public sealed class ServiceUnavailableException : AppException
{
    public ServiceUnavailableException(string message)
        : base(message)
    {
    }
}
