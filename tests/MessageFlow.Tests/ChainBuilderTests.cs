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

    [Fact]
    public async Task UseBranch_MatchingRequest_EntersTheBranch()
    {
        var chain = Chain.Create<int, string>()
            .UseBranch(
                request => request < 0,
                branch => branch.UseWhen(request => request == -1, (_, _) => new ValueTask<string>("minus one")))
            .WithFallback((_, _) => new ValueTask<string>("trunk"))
            .Build();

        Assert.Equal(1, chain.Count);
        Assert.Equal("minus one", await chain.ExecuteAsync(-1));
    }

    [Fact]
    public async Task UseBranch_NonMatchingRequest_SkipsTheBranch()
    {
        var entered = false;

        var chain = Chain.Create<int, string>()
            .UseBranch(
                request => request < 0,
                branch => branch.Use((_, _, _) =>
                {
                    entered = true;
                    return new ValueTask<string>("branch");
                }))
            .WithFallback((_, _) => new ValueTask<string>("trunk"))
            .Build();

        Assert.Equal("trunk", await chain.ExecuteAsync(1));
        Assert.False(entered);
    }

    [Fact]
    public async Task UseBranch_UnhandledByTheBranch_FallsThroughToTheTrunk()
    {
        var chain = Chain.Create<int, string>()
            .UseBranch(
                request => request < 0,
                branch => branch.UseWhen(request => request == -1, (_, _) => new ValueTask<string>("minus one")))
            .UseWhen(request => request < 0, (request, _) => new ValueTask<string>($"trunk:{request}"))
            .Build();

        Assert.Equal("trunk:-2", await chain.ExecuteAsync(-2));
    }

    [Fact]
    public async Task UseBranch_WithOwnFallback_DoesNotFallThroughToTheTrunk()
    {
        var chain = Chain.Create<int, string>()
            .UseBranch(
                request => request < 0,
                branch => branch
                    .UseWhen(request => request == -1, (_, _) => new ValueTask<string>("minus one"))
                    .WithFallback((request, _) => new ValueTask<string>($"branch:{request}")))
            .WithFallback((request, _) => new ValueTask<string>($"trunk:{request}"))
            .Build();

        Assert.Equal("branch:-2", await chain.ExecuteAsync(-2));
        Assert.Equal("trunk:2", await chain.ExecuteAsync(2));
    }

    [Fact]
    public async Task UseBranch_PropagatesCancellationTokenIntoTheBranch()
    {
        using var cts = new CancellationTokenSource();

        var chain = Chain.Create<string, bool>()
            .UseBranch(
                _ => true,
                branch => branch.Use((_, _, cancellationToken) =>
                    new ValueTask<bool>(cancellationToken.IsCancellationRequested)))
            .WithFallback((_, _) => new ValueTask<bool>(false))
            .Build();

        Assert.False(await chain.ExecuteAsync("x", cts.Token));

        cts.Cancel();
        Assert.True(await chain.ExecuteAsync("x", cts.Token));
    }

    [Fact]
    public void UseBranch_ConfiguresTheBranchOnce()
    {
        var configureCount = 0;

        var builder = Chain.Create<int, string>()
            .UseBranch(
                _ => true,
                branch =>
                {
                    configureCount++;
                    branch.Use((request, _, _) => new ValueTask<string>($"branch:{request}"));
                });

        builder.Build();
        builder.Build();

        Assert.Equal(1, configureCount);
    }

    [Fact]
    public void UseBranch_NullArguments_Throw()
    {
        var builder = Chain.Create<int, string>();

        Assert.Throws<ArgumentNullException>(() => builder.UseBranch(null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => builder.UseBranch(_ => true, null!));
    }

    [Fact]
    public async Task Use_MergedBuilder_HandlesTheRequest()
    {
        var fragment = Chain.Create<int, string>()
            .UseWhen(request => request == 1, (_, _) => new ValueTask<string>("fragment:one"));

        var chain = Chain.Create<int, string>()
            .Use(fragment)
            .WithFallback((_, _) => new ValueTask<string>("trunk"))
            .Build();

        Assert.Equal(1, chain.Count);
        Assert.Equal("fragment:one", await chain.ExecuteAsync(1));
    }

    [Fact]
    public async Task Use_MergedBuilder_UnhandledRequest_FallsThroughToTheTrunk()
    {
        var fragment = Chain.Create<int, string>()
            .UseWhen(request => request == 1, (_, _) => new ValueTask<string>("fragment:one"));

        var withFallback = Chain.Create<int, string>()
            .Use(fragment)
            .UseWhen(request => request == 2, (_, _) => new ValueTask<string>("trunk:two"))
            .WithFallback((request, _) => new ValueTask<string>($"trunk fallback:{request}"))
            .Build();

        var withoutFallback = Chain.Create<int, string>()
            .Use(fragment)
            .Build();

        Assert.Equal("trunk:two", await withFallback.ExecuteAsync(2));
        Assert.Equal("trunk fallback:3", await withFallback.ExecuteAsync(3));
        await Assert.ThrowsAsync<UnhandledRequestException>(async () => await withoutFallback.ExecuteAsync(3));
    }

    [Fact]
    public async Task Use_MergedBuilder_WithOwnFallback_DoesNotFallThroughToTheTrunk()
    {
        var fragment = Chain.Create<int, string>()
            .UseWhen(request => request == 1, (_, _) => new ValueTask<string>("fragment:one"))
            .WithFallback((request, _) => new ValueTask<string>($"fragment fallback:{request}"));

        var chain = Chain.Create<int, string>()
            .Use(fragment)
            .UseWhen(_ => true, (_, _) => new ValueTask<string>("trunk"))
            .Build();

        Assert.Equal("fragment fallback:3", await chain.ExecuteAsync(3));
    }

    [Fact]
    public async Task Use_MergedBuilder_PreservesHandlerOrderAcrossTheSeam()
    {
        var log = new List<string>();

        var fragment = Chain.Create<string, string>()
            .Use(async (request, next, cancellationToken) =>
            {
                log.Add("fragment:before");
                var response = await next(request, cancellationToken);
                log.Add("fragment:after");
                return response;
            });

        var chain = Chain.Create<string, string>()
            .Use(async (request, next, cancellationToken) =>
            {
                log.Add("trunk:before");
                var response = await next(request, cancellationToken);
                log.Add("trunk:after");
                return response;
            })
            .Use(fragment)
            .WithFallback((request, _) => new ValueTask<string>(request))
            .Build();

        Assert.Equal("request", await chain.ExecuteAsync("request"));
        Assert.Equal(["trunk:before", "fragment:before", "fragment:after", "trunk:after"], log);
    }

    [Fact]
    public async Task Use_MergedBuilder_SnapshotsTheFragmentAtMergeTime()
    {
        var fragment = Chain.Create<int, string>()
            .UseWhen(request => request == 1, (_, _) => new ValueTask<string>("fragment:one"));

        var builder = Chain.Create<int, string>()
            .Use(fragment)
            .WithFallback((_, _) => new ValueTask<string>("trunk"));

        fragment.UseWhen(request => request == 2, (_, _) => new ValueTask<string>("fragment:two"));

        var chain = builder.Build();

        Assert.Equal("fragment:one", await chain.ExecuteAsync(1));
        Assert.Equal("trunk", await chain.ExecuteAsync(2));
    }

    [Fact]
    public async Task Use_MergedBuilder_CanBeMergedIntoSeveralChains()
    {
        var fragment = Chain.Create<int, string>()
            .UseWhen(request => request == 1, (_, _) => new ValueTask<string>("fragment:one"));

        var first = Chain.Create<int, string>()
            .Use(fragment)
            .WithFallback((_, _) => new ValueTask<string>("first"))
            .Build();

        var second = Chain.Create<int, string>()
            .Use(fragment)
            .WithFallback((_, _) => new ValueTask<string>("second"))
            .Build();

        Assert.Equal("fragment:one", await first.ExecuteAsync(1));
        Assert.Equal("fragment:one", await second.ExecuteAsync(1));
        Assert.Equal("first", await first.ExecuteAsync(9));
        Assert.Equal("second", await second.ExecuteAsync(9));
    }

    [Fact]
    public async Task Use_BuilderMergedIntoItself_TerminatesAndKeepsTheSnapshot()
    {
        var builder = Chain.Create<int, string>()
            .UseWhen(request => request == 1, (_, _) => new ValueTask<string>("one"));

        builder.Use(builder);

        var chain = builder.WithFallback((_, _) => new ValueTask<string>("fallback")).Build();

        Assert.Equal(2, chain.Count);
        Assert.Equal("one", await chain.ExecuteAsync(1));
        Assert.Equal("fallback", await chain.ExecuteAsync(2));
    }

    [Fact]
    public async Task Use_MergedChain_BehavesLikeTheMergedBuilder()
    {
        var fragment = Chain.Create<int, string>()
            .UseWhen(request => request == 1, (_, _) => new ValueTask<string>("fragment:one"))
            .Build();

        var chain = Chain.Create<int, string>()
            .Use(fragment)
            .UseWhen(request => request == 2, (_, _) => new ValueTask<string>("trunk:two"))
            .WithFallback((request, _) => new ValueTask<string>($"trunk fallback:{request}"))
            .Build();

        Assert.Equal(2, chain.Count);
        Assert.Equal("fragment:one", await chain.ExecuteAsync(1));
        Assert.Equal("trunk:two", await chain.ExecuteAsync(2));
        Assert.Equal("trunk fallback:3", await chain.ExecuteAsync(3));
        await Assert.ThrowsAsync<UnhandledRequestException>(async () => await fragment.ExecuteAsync(3));
    }

    [Fact]
    public async Task Use_MergedChain_WithOwnFallback_DoesNotFallThroughToTheTrunk()
    {
        var fragment = Chain.Create<int, string>()
            .UseWhen(request => request == 1, (_, _) => new ValueTask<string>("fragment:one"))
            .WithFallback((request, _) => new ValueTask<string>($"fragment fallback:{request}"))
            .Build();

        var chain = Chain.Create<int, string>()
            .Use(fragment)
            .UseWhen(_ => true, (_, _) => new ValueTask<string>("trunk"))
            .Build();

        Assert.Equal("fragment fallback:3", await chain.ExecuteAsync(3));
    }

    [Fact]
    public async Task Use_CustomChainImplementation_TerminatesTheChain()
    {
        var chain = Chain.Create<int, string>()
            .Use(new ConstantChain())
            .UseWhen(_ => true, (_, _) => new ValueTask<string>("trunk"))
            .Build();

        Assert.Equal(2, chain.Count);
        Assert.Equal("constant:5", await chain.ExecuteAsync(5));
    }

    [Fact]
    public void Use_NullMergeArguments_Throw()
    {
        var builder = Chain.Create<int, string>();

        Assert.Throws<ArgumentNullException>(() => builder.Use((ChainBuilder<int, string>)null!));
        Assert.Throws<ArgumentNullException>(() => builder.Use((IChain<int, string>)null!));
    }

    private sealed class ConstantChain : IChain<int, string>
    {
        public int Count => 1;

        public ValueTask<string> ExecuteAsync(int request, CancellationToken cancellationToken = default)
            => new($"constant:{request}");
    }

    private sealed class UpperCaseHandler : HandlerBase<string, string>
    {
        protected override bool CanHandle(string request) => request.All(char.IsLetter);

        protected override ValueTask<string> ProcessAsync(string request, CancellationToken cancellationToken)
            => new(request.ToUpperInvariant());
    }
}
