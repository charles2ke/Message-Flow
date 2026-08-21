namespace MessageFlow.Tests;

public sealed class HandlerBaseTests
{
    [Fact]
    public async Task HandleAsync_WhenCanHandle_ProcessesRequest()
    {
        var handler = new EvenNumberHandler();

        var response = await handler.HandleAsync(2, (_, _) => new ValueTask<string>("nextHandler"), CancellationToken.None);

        Assert.Equal("even:2", response);
    }

    [Fact]
    public async Task HandleAsync_WhenCannotHandle_CallsNext()
    {
        var handler = new EvenNumberHandler();

        var response = await handler.HandleAsync(3, (_, _) => new ValueTask<string>("nextHandler"), CancellationToken.None);

        Assert.Equal("nextHandler", response);
    }

    [Fact]
    public void HandleAsync_NullNext_Throws()
    {
        var handler = new EvenNumberHandler();

        Assert.Throws<ArgumentNullException>(() => handler.HandleAsync(2, null!, CancellationToken.None));
    }

    private sealed class EvenNumberHandler : HandlerBase<int, string>
    {
        protected override bool CanHandle(int request) => request % 2 == 0;

        protected override ValueTask<string> ProcessAsync(int request, CancellationToken cancellationToken)
            => new($"even:{request}");
    }
}
