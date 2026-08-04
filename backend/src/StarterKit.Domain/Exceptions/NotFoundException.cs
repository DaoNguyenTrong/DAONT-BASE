namespace StarterKit.Domain.Exceptions;

public sealed class NotFoundException : AppException
{
    public NotFoundException(string message)
        : base(message)
    {
        Args = [];
    }

    public NotFoundException(string entityName, object id)
        : base("EntityNotFound")
    {
        Args = [entityName, id];
    }

    public object[] Args { get; }
}
