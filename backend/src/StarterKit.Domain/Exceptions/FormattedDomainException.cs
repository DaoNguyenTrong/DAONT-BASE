namespace StarterKit.Domain.Exceptions;

public sealed class FormattedDomainException(string key, params object[] args) : ApiException(key)
{
    public object[] Args { get; } = args;

    public override int StatusCode => 400;

    public override string Title => "Bad Request";
}
