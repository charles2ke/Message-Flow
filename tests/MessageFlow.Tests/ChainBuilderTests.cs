using MessageFlow;

namespace MessageFlow.Tests;

public sealed class ChainBuilderTests
{
    [Fact]
    public void Create_ReturnsEmptyBuilder()
    {
        var chain = Chain.Create<string, string>()
            .WithFallback((_, _) => new ValueTask<string>("fallback"))
            .Build();

        Assert.Equal(0, chain.Count);
    }

    [Fact]
    public async Task ExecuteAsync_UsesFirstMatchingHandler()
    {
        var chain = Chain.Create<int, string>()
            .UseWhen(request => request < 0, (_, _) => new ValueTask<string>("negative"))
            .UseWhen(request => request == 0, (_, _) => new ValueTask<string>("zero"))
            .UseWhen(request => request > 0, (_, _) => new ValueTask<string>("positive"))
            .Build();

        Assert.Equal(3, chain.Count);
        Assert.Equal("negative", await chain.ExecuteAsync(-1));
        Assert.Equal("zero", await chain.ExecuteAsync(0));
        Assert.Equal("positive", await chain.ExecuteAsync(1));
    }

    [Fact]
    public async Task ExecuteAsync_WithoutFallback_ThrowsUnhandledRequestException()
    {
        var chain = Chain.Create<int, string>()
            .UseWhen(request => request > 0, (_, _) => new ValueTask<string>("positive"))
            .Build();

        var exception = await Assert.ThrowsAsync<UnhandledRequestException>(
            async () => await chain.ExecuteAsync(-5));

        Assert.Equal("No handler in the chain handled the request.", exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithFallback_ReturnsFallbackResponse()
    {
        var chain = Chain.Create<int, string>()
            .UseWhen(request => request > 0, (_, _) => new ValueTask<string>("positive"))
            .WithFallback((request, _) => new ValueTask<string>($"fallback:{request}"))
            .Build();

        Assert.Equal("fallback:-5", await chain.ExecuteAsync(-5));
    }

    [Fact]
    public async Task Use_InlineHandler_CanDecorateTheRestOfTheChain()
    {
        var log = new List<string>();

        var chain = Chain.Create<string, string>()
            .Use(async (request, nextHandler, cancellationToken) =>
            {
                log.Add("before");
                var response = await nextHandler(request, cancellationToken);
                log.Add("after");
                return response.ToUpperInvariant();
            })
            .Use((request, _, _) =>
            {
                log.Add("handled");
                return new ValueTask<string>(request);
            })
            .Build();

        Assert.Equal("HELLO", await chain.ExecuteAsync("hello"));
        Assert.Equal(["before", "handled", "after"], log);
    }

    [Fact]
    public async Task Use_HandlerInstance_IsInvoked()
    {
        var chain = Chain.Create<string, string>()
            .Use(new UpperCaseHandler())
            .WithFallback((_, _) => new ValueTask<string>("fallback"))
            .Build();

        Assert.Equal("ABC", await chain.ExecuteAsync("abc"));
        Assert.Equal("fallback", await chain.ExecuteAsync("123"));
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationTokenThroughTheChain()
    {
        using var cts = new CancellationTokenSource();

        var chain = Chain.Create<string, bool>()
            .Use((request, nextHandler, cancellationToken) => nextHandler(request, cancellationToken))
            .WithFallback((_, cancellationToken) => new ValueTask<bool>(cancellationToken.IsCancellationRequested))
            .Build();

        Assert.False(await chain.ExecuteAsync("x", cts.Token));

        cts.Cancel();
        Assert.True(await chain.ExecuteAsync("x", cts.Token));
    }

    [Fact]
    public void Build_TakesSnapshotOfHandlers()
    {
        var builder = Chain.Create<int, string>()
            .UseWhen(_ => true, (_, _) => new ValueTask<string>("first"));

        var chain = builder.Build();

        builder.UseWhen(_ => true, (_, _) => new ValueTask<string>("second"));

        Assert.Equal(1, chain.Count);
        Assert.Equal(2, builder.Build().Count);
    }

    [Fact]
    public void Use_NullHandlerInstance_Throws()
    {
        var builder = Chain.Create<int, string>();

        Assert.Throws<ArgumentNullException>(() => builder.Use((IHandler<int, string>)null!));
    }

    [Fact]
    public void Use_NullInlineHandler_Throws()
    {
        var builder = Chain.Create<int, string>();

        Assert.Throws<ArgumentNullException>(() =>
            builder.Use((Func<int, NextHandler<int, string>, CancellationToken, ValueTask<string>>)null!));
    }

    [Fact]
    public void UseWhen_NullArguments_Throw()
    {
        var builder = Chain.Create<int, string>();

        Assert.Throws<ArgumentNullException>(() => builder.UseWhen(null!, (_, _) => new ValueTask<string>("x")));
        Assert.Throws<ArgumentNullException>(() => builder.UseWhen(_ => true, null!));
    }

    [Fact]
    public void WithFallback_Null_Throws()
    {
        var builder = Chain.Create<int, string>();

        Assert.Throws<ArgumentNullException>(() => builder.WithFallback(null!));
    }

    private sealed class UpperCaseHandler : HandlerBase<string, string>
    {
        protected override bool CanHandle(string request) => request.All(char.IsLetter);

        protected override ValueTask<string> ProcessAsync(string request, CancellationToken cancellationToken)
            => new(request.ToUpperInvariant());
    }
}
