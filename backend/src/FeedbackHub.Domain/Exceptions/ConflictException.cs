namespace FeedbackHub.Domain.Exceptions;

public sealed class ConflictException : ApiException
{
    public ConflictException(string message)
        : base(message)
    {
    }

    public override int StatusCode => 409;

    public override string Title => "Conflict";
}
