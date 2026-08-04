namespace StarterKit.Domain.Exceptions;

public sealed class FormattedDomainException(string key, params object[] args) : AppException(key)
{
    public object[] Args { get; } = args;
}
