namespace StarterKit.Domain.Exceptions;

public sealed class UnauthorizedException : AppException
{
    public UnauthorizedException(string message)
        : base(message)
    {
    }
}
