using MessageFlow.Samples;

namespace MessageFlow.Tests;

public sealed class SamplesTests
{
    [Fact]
    public async Task QuickStartSample_ClassifiesEveryRequest()
    {
        await using var output = new StringWriter();

        var results = await QuickStartSample.RunAsync(output);

        Assert.Equal(["negative:-7", "zero", "positive:42"], results);
        Assert.Contains("42 => positive:42", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task QuickStartSample_RunAsync_RequiresOutput()
        => await Assert.ThrowsAsync<ArgumentNullException>(async () => await QuickStartSample.RunAsync(null!));

    [Fact]
    public async Task SupportTicketSample_RoutesOrEscalatesEveryTicket()
    {
        await using var output = new StringWriter();

        var results = await SupportTicketSample.RunAsync(output);

        Assert.Equal(
            [
                "refund issued for ticket 1",
                "reset link sent for ticket 2",
                "escalated ticket 3 to a human",
            ],
            results);
    }

    [Fact]
    public async Task SupportTicketSample_ChainHasOneHandlerPerTicketKind()
    {
        var chain = SupportTicketSample.BuildChain();

        Assert.Equal(2, chain.Count);
        Assert.Equal("refund issued for ticket 7", await chain.ExecuteAsync(new Ticket(7, TicketKind.Refund)));
    }

    [Fact]
    public async Task SupportTicketSample_RunAsync_RequiresOutput()
        => await Assert.ThrowsAsync<ArgumentNullException>(async () => await SupportTicketSample.RunAsync(null!));

    [Fact]
    public void SupportTicketSample_HandlersRejectNullTickets()
    {
        var refund = new RefundHandler();
        var passwordReset = new PasswordResetHandler();
        NextHandler<Ticket, string> next = (_, _) => new ValueTask<string>("next");

        Assert.Throws<ArgumentNullException>(() => refund.HandleAsync(null!, next, default));
        Assert.Throws<ArgumentNullException>(() => passwordReset.HandleAsync(null!, next, default));
    }

    [Fact]
    public async Task MiddlewareSample_LogsBeforeAndAfterTheInnerChain()
    {
        await using var output = new StringWriter();

        var log = await MiddlewareSample.RunAsync(output);

        Assert.Equal(
            [
                "before:hello world",
                "after:GREETING HANDLED: HELLO WORLD",
                "before:ping",
                "after:ECHO: PING",
            ],
            log);
    }

    [Fact]
    public async Task MiddlewareSample_RunAsync_RequiresOutput()
        => await Assert.ThrowsAsync<ArgumentNullException>(async () => await MiddlewareSample.RunAsync(null!));

    [Fact]
    public void MiddlewareSample_BuildChain_RequiresLog()
        => Assert.Throws<ArgumentNullException>(() => MiddlewareSample.BuildChain(null!));

    [Fact]
    public async Task UnhandledRequestSample_ThrowsWithoutFallbackAndRecoversWithOne()
    {
        await using var output = new StringWriter();

        var results = await UnhandledRequestSample.RunAsync(output);

        Assert.Equal(
            [
                "handled:1",
                "No handler in the chain handled the request.",
                "unhandled, using default",
            ],
            results);
    }

    [Fact]
    public async Task UnhandledRequestSample_HandlesPositiveRequestsWithoutFallback()
    {
        var chain = UnhandledRequestSample.BuildChain(withFallback: false);

        Assert.Equal("handled:3", await UnhandledRequestSample.DescribeAsync(chain, 3));
    }

    [Fact]
    public async Task UnhandledRequestSample_DescribeAsync_RequiresChain()
        => await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await UnhandledRequestSample.DescribeAsync(null!, 1));

    [Fact]
    public async Task UnhandledRequestSample_RunAsync_RequiresOutput()
        => await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await UnhandledRequestSample.RunAsync(null!));

    [Fact]
    public async Task CancellationSample_CompletesAndObservesCancellation()
    {
        await using var output = new StringWriter();

        var results = await CancellationSample.RunAsync(output);

        Assert.Equal(["processed:job", nameof(OperationCanceledException)], results);
    }

    [Fact]
    public async Task CancellationSample_DescribeAsync_RequiresChain()
        => await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await CancellationSample.DescribeAsync(null!, "job"));

    [Fact]
    public async Task CancellationSample_RunAsync_RequiresOutput()
        => await Assert.ThrowsAsync<ArgumentNullException>(async () => await CancellationSample.RunAsync(null!));

    [Fact]
    public async Task RetrySample_RetriesUntilSuccessThenGivesUp()
    {
        await using var output = new StringWriter();

        var results = await RetrySample.RunAsync(output);

        Assert.Equal(["completed:import after 2 failure(s)", "transient failure 2"], results);
    }

    [Fact]
    public async Task RetrySample_DescribeAsync_RequiresChain()
        => await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await RetrySample.DescribeAsync(null!, "import"));

    [Fact]
    public async Task RetrySample_RunAsync_RequiresOutput()
        => await Assert.ThrowsAsync<ArgumentNullException>(async () => await RetrySample.RunAsync(null!));

    [Fact]
    public async Task RetryHandler_RequiresNextHandler()
    {
        var handler = new RetryHandler(maxAttempts: 1);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await handler.HandleAsync("request", null!, default));
    }

    [Fact]
    public async Task Program_RunAllAsync_RunsEverySample()
    {
        await using var output = new StringWriter();

        await Program.RunAllAsync(output);

        var text = output.ToString();
        Assert.Contains("== quick start ==", text, StringComparison.Ordinal);
        Assert.Contains("== support tickets ==", text, StringComparison.Ordinal);
        Assert.Contains("== middleware ==", text, StringComparison.Ordinal);
        Assert.Contains("== unhandled requests ==", text, StringComparison.Ordinal);
        Assert.Contains("== cancellation ==", text, StringComparison.Ordinal);
        Assert.Contains("== retry ==", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Program_RunAllAsync_RequiresOutput()
        => await Assert.ThrowsAsync<ArgumentNullException>(async () => await Program.RunAllAsync(null!));
}
