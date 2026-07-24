namespace FeedbackHub.Domain.Exceptions;

public sealed class ForbiddenException : ApiException
{
    public ForbiddenException(string message)
        : base(message)
    {
    }

    public override int StatusCode => 403;

    public override string Title => "Forbidden";
}
