using System.Diagnostics;

namespace MessageFlow.Tests;

[Collection(DiagnosticsCollection.Name)]
public sealed class DiagnosticsTests
{
    [Fact]
    public async Task UseLogging_LogsStartAndCompletion()
    {
        var logger = new RecordingLogger();

        var chain = Chain.Create<int, string>()
            .UseLogging(logger)
            .WithFallback((request, _) => new ValueTask<string>($"handled:{request}"))
            .Build();

        Assert.Equal("handled:7", await chain.ExecuteAsync(7));
        Assert.Equal(2, logger.Entries.Count);
        Assert.All(logger.Entries, entry => Assert.Equal(ChainLogLevel.Debug, entry.Level));
        Assert.Contains("Executing chain Int32 -> String.", logger.Entries[0].Message, StringComparison.Ordinal);
        Assert.Contains("Executed chain Int32 -> String in", logger.Entries[1].Message, StringComparison.Ordinal);
        Assert.All(logger.Entries, entry => Assert.Null(entry.Exception));
    }

    [Fact]
    public async Task UseLogging_HonoursTheConfiguredLevel()
    {
        var logger = new RecordingLogger();

        var chain = Chain.Create<int, string>()
            .UseLogging(logger, ChainLogLevel.Information)
            .WithFallback((_, _) => new ValueTask<string>("handled"))
            .Build();

        await chain.ExecuteAsync(1);

        Assert.All(logger.Entries, entry => Assert.Equal(ChainLogLevel.Information, entry.Level));
    }

    [Fact]
    public async Task UseLogging_WhenLevelIsDisabled_WritesNothing()
    {
        var logger = new RecordingLogger { EnabledLevel = null };

        var chain = Chain.Create<int, string>()
            .UseLogging(logger)
            .WithFallback((_, _) => new ValueTask<string>("handled"))
            .Build();

        await chain.ExecuteAsync(1);

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task UseLogging_WhenTheChainFails_LogsTheErrorAndRethrows()
    {
        var logger = new RecordingLogger();

        var chain = Chain.Create<int, string>()
            .UseLogging(logger)
            .Use((_, _, _) => throw new InvalidOperationException("boom"))
            .Build();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await chain.ExecuteAsync(1));

        Assert.Equal("boom", exception.Message);
        var error = Assert.Single(logger.Entries, entry => entry.Level == ChainLogLevel.Error);
        Assert.Contains("Chain Int32 -> String failed after", error.Message, StringComparison.Ordinal);
        Assert.Same(exception, error.Exception);
    }

    [Fact]
    public async Task UseLogging_WhenTheChainFailsAndErrorsAreDisabled_WritesNothing()
    {
        var logger = new RecordingLogger { EnabledLevel = null };

        var chain = Chain.Create<int, string>()
            .UseLogging(logger)
            .Use((_, _, _) => throw new InvalidOperationException("boom"))
            .Build();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await chain.ExecuteAsync(1));

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void UseLogging_RequiresBuilderAndLogger()
    {
        Assert.Throws<ArgumentNullException>(
            () => ChainBuilderDiagnosticsExtensions.UseLogging<int, string>(null!, new RecordingLogger()));
        Assert.Throws<ArgumentNullException>(() => Chain.Create<int, string>().UseLogging(null!));
    }

    [Fact]
    public async Task UseTracing_RecordsASuccessfulActivity()
    {
        using var listener = new CollectingListener();

        var chain = Chain.Create<int, string>()
            .UseTracing()
            .WithFallback((request, _) => new ValueTask<string>($"handled:{request}"))
            .Build();

        Assert.Equal("handled:7", await chain.ExecuteAsync(7));

        var activity = Assert.Single(listener.Activities);
        Assert.Equal(ChainDiagnostics.ExecuteActivityName, activity.OperationName);
        Assert.Equal(ActivityKind.Internal, activity.Kind);
        Assert.Equal(ActivityStatusCode.Ok, activity.Status);
        Assert.Equal(typeof(int).FullName, activity.GetTagItem(ChainDiagnostics.RequestTypeTag));
        Assert.Equal(typeof(string).FullName, activity.GetTagItem(ChainDiagnostics.ResponseTypeTag));
    }

    [Fact]
    public async Task UseTracing_UsesTheConfiguredActivityName()
    {
        using var listener = new CollectingListener();

        var chain = Chain.Create<int, string>()
            .UseTracing("tickets")
            .WithFallback((_, _) => new ValueTask<string>("handled"))
            .Build();

        await chain.ExecuteAsync(1);

        Assert.Equal("tickets", Assert.Single(listener.Activities).OperationName);
    }

    [Fact]
    public async Task UseTracing_WhenTheChainFails_RecordsTheErrorAndRethrows()
    {
        using var listener = new CollectingListener();

        var chain = Chain.Create<int, string>()
            .UseTracing()
            .Use((_, _, _) => throw new InvalidOperationException("boom"))
            .Build();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await chain.ExecuteAsync(1));

        var activity = Assert.Single(listener.Activities);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("boom", activity.StatusDescription);
        var recorded = Assert.Single(activity.Events);
        Assert.Equal("exception", recorded.Name);
        Assert.Contains(recorded.Tags, tag => tag is { Key: "exception.message", Value: "boom" });
        Assert.Contains(
            recorded.Tags,
            tag => tag.Key == "exception.type" && (string?)tag.Value == typeof(InvalidOperationException).FullName);
    }

    [Fact]
    public async Task UseTracing_WithoutListener_ExecutesTheChain()
    {
        var chain = Chain.Create<int, string>()
            .UseTracing()
            .WithFallback((request, _) => new ValueTask<string>($"handled:{request}"))
            .Build();

        Assert.Equal("handled:7", await chain.ExecuteAsync(7));
    }

    [Fact]
    public async Task UseTracing_WithoutListener_PropagatesFailures()
    {
        var chain = Chain.Create<int, string>()
            .UseTracing()
            .Use((_, _, _) => throw new InvalidOperationException("boom"))
            .Build();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await chain.ExecuteAsync(1));
    }

    [Fact]
    public void UseTracing_RequiresBuilderAndActivityName()
    {
        Assert.Throws<ArgumentNullException>(() => ChainBuilderDiagnosticsExtensions.UseTracing<int, string>(null!));
        Assert.Throws<ArgumentNullException>(() => Chain.Create<int, string>().UseTracing(null!));
        Assert.Throws<ArgumentException>(() => Chain.Create<int, string>().UseTracing(string.Empty));
    }

    [Fact]
    public void ActivitySource_IsNamedAfterTheLibrary()
    {
        Assert.Equal(ChainDiagnostics.ActivitySourceName, ChainDiagnostics.ActivitySource.Name);
        Assert.Equal(ChainDiagnostics.ActivitySourceVersion, ChainDiagnostics.ActivitySource.Version);
    }

    private sealed record LogEntry(ChainLogLevel Level, string Message, Exception? Exception);

    private sealed class RecordingLogger : IChainLogger
    {
        public ChainLogLevel? EnabledLevel { get; init; } = ChainLogLevel.Trace;

        public List<LogEntry> Entries { get; } = [];

        public bool IsEnabled(ChainLogLevel level) => EnabledLevel is { } enabled && level >= enabled;

        public void Log(ChainLogLevel level, string message, Exception? exception)
            => Entries.Add(new LogEntry(level, message, exception));
    }

    private sealed class CollectingListener : IDisposable
    {
        private readonly ActivityListener _listener;

        public CollectingListener()
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == ChainDiagnostics.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = Activities.Add,
            };

            ActivitySource.AddActivityListener(_listener);
        }

        public List<Activity> Activities { get; } = [];

        public void Dispose() => _listener.Dispose();
    }
}
