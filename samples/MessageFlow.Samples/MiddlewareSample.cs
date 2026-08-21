using MessageFlow;

namespace MessageFlow.Samples;

/// <summary>
/// Shows middleware-style handlers that run code before <em>and</em> after the rest of the chain.
/// </summary>
public static class MiddlewareSample
{
    /// <summary>
    /// Builds a chain that logs every request, upper-cases the response of the inner chain and
    /// answers greetings.
    /// </summary>
    /// <param name="log">Receives one entry per middleware step.</param>
    /// <returns>The composed chain.</returns>
    public static IChain<string, string> BuildChain(ICollection<string> log)
    {
        ArgumentNullException.ThrowIfNull(log);

        return Chain.Create<string, string>()
            .Use(async (request, next, cancellationToken) =>
            {
                log.Add($"before:{request}");
                var response = await next(request, cancellationToken).ConfigureAwait(false);
                log.Add($"after:{response}");
                return response;
            })
            .Use(async (request, next, cancellationToken) =>
            {
                var response = await next(request, cancellationToken).ConfigureAwait(false);
                return response.ToUpperInvariant();
            })
            .UseWhen(
                request => request.StartsWith("hello", StringComparison.OrdinalIgnoreCase),
                (request, _) => new ValueTask<string>($"greeting handled: {request}"))
            .WithFallback((request, _) => new ValueTask<string>($"echo: {request}"))
            .Build();
    }

    /// <summary>
    /// Runs the sample for a greeting and for an unrelated request.
    /// </summary>
    /// <param name="output">The writer receiving the sample output.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The middleware log produced by both requests.</returns>
    public static async Task<IReadOnlyList<string>> RunAsync(
        TextWriter output,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);

        var log = new List<string>();
        var chain = BuildChain(log);

        foreach (var request in new[] { "hello world", "ping" })
        {
            var response = await chain.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync($"{request} => {response}").ConfigureAwait(false);
        }

        foreach (var entry in log)
        {
            await output.WriteLineAsync($"  log: {entry}").ConfigureAwait(false);
        }

        return log;
    }
}
