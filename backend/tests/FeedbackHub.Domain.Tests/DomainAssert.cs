using FeedbackHub.Domain.Exceptions;

namespace FeedbackHub.Domain.Tests;

public static class DomainAssert
{
    public static void ThrowsWithMessage(string expectedMessage, Action act)
    {
        DomainException ex = Assert.Throws<DomainException>(act);
        Assert.Equal(expectedMessage, ex.Message);
    }
}
