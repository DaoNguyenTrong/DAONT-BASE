namespace FeedbackHub.Domain.Exceptions;

public abstract class ApiException : Exception
{
    protected ApiException(string message)
        : base(message)
    {
    }

    public abstract int StatusCode { get; }

    public abstract string Title { get; }
}
