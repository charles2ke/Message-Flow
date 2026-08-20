namespace MessageFlow.Tests;

public sealed class UnhandledRequestExceptionTests
{
    [Fact]
    public void DefaultConstructor_SetsDefaultMessage()
        => Assert.Equal("No handler in the chain handled the request.", new UnhandledRequestException().Message);

    [Fact]
    public void MessageConstructor_SetsMessage()
        => Assert.Equal("boom", new UnhandledRequestException("boom").Message);

    [Fact]
    public void InnerExceptionConstructor_SetsMessageAndInnerException()
    {
        var inner = new InvalidOperationException("inner");

        var exception = new UnhandledRequestException("boom", inner);

        Assert.Equal("boom", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }
}
