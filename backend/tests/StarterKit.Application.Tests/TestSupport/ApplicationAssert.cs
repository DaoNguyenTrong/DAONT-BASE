using StarterKit.Domain.Exceptions;

namespace StarterKit.Application.Tests.TestSupport;

public static class ApplicationAssert
{
    public static async Task ThrowsWithMessageAsync<TException>(string expectedMessage, Func<Task> act)
        where TException : AppException
    {
        TException ex = await Assert.ThrowsAsync<TException>(act);
        Assert.Equal(expectedMessage, ex.Message);
    }

    public static async Task AssertNotFoundAsync<TEntity>(object expectedId, Func<Task> act)
    {
        NotFoundException ex = await Assert.ThrowsAsync<NotFoundException>(act);
        Assert.Equal(typeof(TEntity).Name, ex.Args[0]);
        Assert.Equal(expectedId, ex.Args[1]);
    }

    public static async Task AssertNotFoundAsync(string expectedEntityName, object expectedId, Func<Task> act)
    {
        NotFoundException ex = await Assert.ThrowsAsync<NotFoundException>(act);
        Assert.Equal(expectedEntityName, ex.Args[0]);
        Assert.Equal(expectedId, ex.Args[1]);
    }
}
