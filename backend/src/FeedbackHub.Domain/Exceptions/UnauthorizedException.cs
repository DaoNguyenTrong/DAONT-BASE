namespace FeedbackHub.Domain.Exceptions;

public sealed class UnauthorizedException : ApiException
{
    public UnauthorizedException(string message)
        : base(message)
    {
    }

    public override int StatusCode => 401;

    public override string Title => "Unauthorized";
}
