using MessageFlow;

namespace MessageFlow.Samples;

/// <summary>
/// Contrasts a chain without a fallback (which throws <see cref="UnhandledRequestException"/>)
/// with the same chain guarded by a fallback.
/// </summary>
public static class UnhandledRequestSample
{
    /// <summary>
    /// Builds a chain that only understands positive numbers.
    /// </summary>
    /// <param name="withFallback">
    /// When <see langword="true"/>, unhandled requests produce a default response instead of throwing.
    /// </param>
    /// <returns>The composed chain.</returns>
    public static IChain<int, string> BuildChain(bool withFallback)
    {
        var builder = Chain.Create<int, string>()
            .UseWhen(request => request > 0, (request, _) => new ValueTask<string>($"handled:{request}"));

        if (withFallback)
        {
            builder = builder.WithFallback((_, _) => new ValueTask<string>("unhandled, using default"));
        }

        return builder.Build();
    }

    /// <summary>
    /// Executes a request and turns an <see cref="UnhandledRequestException"/> into its message.
    /// </summary>
    /// <param name="chain">The chain to execute.</param>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The response, or the message of the thrown exception.</returns>
    public static async Task<string> DescribeAsync(
        IChain<int, string> chain,
        int request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chain);

        try
        {
            return await chain.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (UnhandledRequestException exception)
        {
            return exception.Message;
        }
    }

    /// <summary>
    /// Runs the sample, showing both the thrown exception and the fallback response.
    /// </summary>
    /// <param name="output">The writer receiving the sample output.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The descriptions produced for every chain and request combination.</returns>
    public static async Task<IReadOnlyList<string>> RunAsync(
        TextWriter output,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);

        var strict = BuildChain(withFallback: false);
        var lenient = BuildChain(withFallback: true);
        var results = new List<string>();

        foreach (var (name, chain, request) in new[]
                 {
                     ("strict", strict, 1),
                     ("strict", strict, -1),
                     ("lenient", lenient, -1),
                 })
        {
            var description = await DescribeAsync(chain, request, cancellationToken).ConfigureAwait(false);
            results.Add(description);
            await output.WriteLineAsync($"{name} {request} => {description}").ConfigureAwait(false);
        }

        return results;
    }
}
