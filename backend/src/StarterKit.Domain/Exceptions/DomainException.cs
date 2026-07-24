namespace StarterKit.Domain.Exceptions;

public sealed class DomainException : ApiException
{
    public DomainException(string message)
        : base(message)
    {
    }

    public override int StatusCode => 400;

    public override string Title => "Bad Request";
}
