using MessageFlow;

namespace MessageFlow.Samples;

/// <summary>
/// Shows how the cancellation token flows from <see cref="IChain{TRequest, TResponse}.ExecuteAsync"/>
/// down to every handler.
/// </summary>
public static class CancellationSample
{
    /// <summary>
    /// Builds a chain whose handler observes the cancellation token before doing any work.
    /// </summary>
    /// <returns>The composed chain.</returns>
    public static IChain<string, string> BuildChain()
        => Chain.Create<string, string>()
            .Use(async (request, next, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return await next(request, cancellationToken).ConfigureAwait(false);
            })
            .WithFallback(async (request, _) =>
            {
                await Task.Yield();
                return $"processed:{request}";
            })
            .Build();

    /// <summary>
    /// Executes a request and turns cancellation into a readable description.
    /// </summary>
    /// <param name="chain">The chain to execute.</param>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The response, or the name of the cancellation exception.</returns>
    public static async Task<string> DescribeAsync(
        IChain<string, string> chain,
        string request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chain);

        try
        {
            return await chain.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return nameof(OperationCanceledException);
        }
    }

    /// <summary>
    /// Runs the sample once with a live token and once with an already cancelled token.
    /// </summary>
    /// <param name="output">The writer receiving the sample output.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The successful response followed by the name of the cancellation exception.</returns>
    public static async Task<IReadOnlyList<string>> RunAsync(
        TextWriter output,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);

        var chain = BuildChain();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(false);

        var results = new List<string>();
        foreach (var token in new[] { cancellationToken, cancelled.Token })
        {
            var description = await DescribeAsync(chain, "job", token).ConfigureAwait(false);
            results.Add(description);
            await output.WriteLineAsync($"job => {description}").ConfigureAwait(false);
        }

        return results;
    }
}
