using System.Diagnostics.CodeAnalysis;

namespace MessageFlow.Samples;

/// <summary>
/// Runs every sample in turn.
/// </summary>
public static class Program
{
    /// <summary>
    /// The entry point of the samples application.
    /// </summary>
    /// <returns>A task that completes when every sample has run.</returns>
    [ExcludeFromCodeCoverage]
    public static Task Main() => RunAllAsync(Console.Out);

    /// <summary>
    /// Runs every sample, writing its output to <paramref name="output"/>.
    /// </summary>
    /// <param name="output">The writer receiving the sample output.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that completes when every sample has run.</returns>
    public static async Task RunAllAsync(TextWriter output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);

        await output.WriteLineAsync("== quick start ==").ConfigureAwait(false);
        await QuickStartSample.RunAsync(output, cancellationToken).ConfigureAwait(false);

        await output.WriteLineAsync("== support tickets ==").ConfigureAwait(false);
        await SupportTicketSample.RunAsync(output, cancellationToken).ConfigureAwait(false);

        await output.WriteLineAsync("== middleware ==").ConfigureAwait(false);
        await MiddlewareSample.RunAsync(output, cancellationToken).ConfigureAwait(false);

        await output.WriteLineAsync("== unhandled requests ==").ConfigureAwait(false);
        await UnhandledRequestSample.RunAsync(output, cancellationToken).ConfigureAwait(false);

        await output.WriteLineAsync("== cancellation ==").ConfigureAwait(false);
        await CancellationSample.RunAsync(output, cancellationToken).ConfigureAwait(false);

        await output.WriteLineAsync("== retry ==").ConfigureAwait(false);
        await RetrySample.RunAsync(output, cancellationToken).ConfigureAwait(false);
    }
}
