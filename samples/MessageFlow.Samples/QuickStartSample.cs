using MessageFlow;

namespace MessageFlow.Samples;

/// <summary>
/// The smallest possible chain: two inline predicates plus a fallback.
/// </summary>
public static class QuickStartSample
{
    /// <summary>
    /// Builds a chain that classifies an integer.
    /// </summary>
    /// <returns>The composed chain.</returns>
    public static IChain<int, string> BuildChain()
        => Chain.Create<int, string>()
            .UseWhen(request => request < 0, (request, _) => new ValueTask<string>($"negative:{request}"))
            .UseWhen(request => request == 0, (_, _) => new ValueTask<string>("zero"))
            .WithFallback((request, _) => new ValueTask<string>($"positive:{request}"))
            .Build();

    /// <summary>
    /// Runs the sample, writing every classification to <paramref name="output"/>.
    /// </summary>
    /// <param name="output">The writer receiving the sample output.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The classifications, in request order.</returns>
    public static async Task<IReadOnlyList<string>> RunAsync(
        TextWriter output,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);

        var chain = BuildChain();
        var results = new List<string>();

        foreach (var request in new[] { -7, 0, 42 })
        {
            var response = await chain.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            results.Add(response);
            await output.WriteLineAsync($"{request} => {response}").ConfigureAwait(false);
        }

        return results;
    }
}
