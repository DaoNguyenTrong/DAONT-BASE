namespace FeedbackHub.Domain.Exceptions;

public sealed class NotFoundException : ApiException
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

    public override int StatusCode => 404;

    public override string Title => "Not Found";
}
