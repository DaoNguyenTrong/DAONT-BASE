namespace StarterKit.Domain.Exceptions;

public sealed class DomainException : AppException
{
    public DomainException(string message)
        : base(message)
    {
    }
}
