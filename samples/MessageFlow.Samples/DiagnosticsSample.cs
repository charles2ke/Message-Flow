using System.Diagnostics;

using MessageFlow;

namespace MessageFlow.Samples;

/// <summary>
/// Shows the built-in logging and tracing middleware, <c>UseLogging</c> and <c>UseTracing</c>.
/// </summary>
public static class DiagnosticsSample
{
    /// <summary>
    /// Builds a chain observed by a logger and by an <see cref="Activity"/> per request.
    /// </summary>
    /// <param name="logger">Receives one entry per request.</param>
    /// <returns>The composed chain.</returns>
    public static IChain<string, string> BuildChain(IChainLogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        return Chain.Create<string, string>()
            .UseLogging(logger, ChainLogLevel.Information)
            .UseTracing("MessageFlow.Samples.Diagnostics")
            .UseWhen(
                request => request.StartsWith("ping", StringComparison.OrdinalIgnoreCase),
                (_, _) => new ValueTask<string>("pong"))
            .WithFallback((request, _) => new ValueTask<string>($"echo: {request}"))
            .Build();
    }

    /// <summary>
    /// Runs the sample with a listener subscribed to the library activity source.
    /// </summary>
    /// <param name="output">The writer receiving the sample output.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The log entries produced by the requests.</returns>
    public static async Task<IReadOnlyList<string>> RunAsync(
        TextWriter output,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);

        var logger = new ConsoleChainLogger();
        var activityNames = new List<string>();

        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ChainDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => activityNames.Add($"{activity.OperationName}:{activity.Status}"),
        };

        ActivitySource.AddActivityListener(listener);

        var chain = BuildChain(logger);

        foreach (var request in new[] { "ping", "hello" })
        {
            var response = await chain.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync($"{request} => {response}").ConfigureAwait(false);
        }

        foreach (var entry in logger.Entries)
        {
            await output.WriteLineAsync($"  log: {entry}").ConfigureAwait(false);
        }

        foreach (var activity in activityNames)
        {
            await output.WriteLineAsync($"  activity: {activity}").ConfigureAwait(false);
        }

        return logger.Entries;
    }

    /// <summary>
    /// A minimal <see cref="IChainLogger"/> collecting the entries in memory.
    /// </summary>
    private sealed class ConsoleChainLogger : IChainLogger
    {
        public List<string> Entries { get; } = [];

        public bool IsEnabled(ChainLogLevel level) => level >= ChainLogLevel.Information;

        public void Log(ChainLogLevel level, string message, Exception? exception)
            => Entries.Add($"{level}: {message}");
    }
}
