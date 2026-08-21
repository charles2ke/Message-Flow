using MessageFlow;

namespace MessageFlow.Samples;

/// <summary>
/// A custom <see cref="IHandler{TRequest, TResponse}"/> that retries the rest of the chain.
/// </summary>
/// <param name="maxAttempts">The maximum number of attempts, including the first one.</param>
public sealed class RetryHandler(int maxAttempts) : IHandler<string, string>
{
    /// <inheritdoc />
    public async ValueTask<string> HandleAsync(
        string request,
        NextHandler<string, string> nextHandler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nextHandler);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await nextHandler(request, cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException) when (attempt < maxAttempts)
            {
                // Try again until the last attempt, which is allowed to fail.
            }
        }
    }
}

/// <summary>
/// Shows a hand-written <see cref="IHandler{TRequest, TResponse}"/> implementing a retry policy
/// around the remainder of the chain.
/// </summary>
public static class RetrySample
{
    /// <summary>
    /// Builds a chain that retries a flaky terminal step.
    /// </summary>
    /// <param name="failuresBeforeSuccess">The number of failures the terminal step produces.</param>
    /// <param name="maxAttempts">The maximum number of attempts performed by the retry handler.</param>
    /// <returns>The composed chain.</returns>
    public static IChain<string, string> BuildChain(int failuresBeforeSuccess, int maxAttempts)
    {
        var failures = 0;

        return Chain.Create<string, string>()
            .Use(new RetryHandler(maxAttempts))
            .WithFallback((request, _) =>
            {
                if (failures < failuresBeforeSuccess)
                {
                    failures++;
                    throw new InvalidOperationException($"transient failure {failures}");
                }

                return new ValueTask<string>($"completed:{request} after {failures} failure(s)");
            })
            .Build();
    }

    /// <summary>
    /// Executes a request and turns a final failure into its message.
    /// </summary>
    /// <param name="chain">The chain to execute.</param>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The response, or the message of the last failure.</returns>
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
        catch (InvalidOperationException exception)
        {
            return exception.Message;
        }
    }

    /// <summary>
    /// Runs the sample once with a recoverable step and once with a permanently failing step.
    /// </summary>
    /// <param name="output">The writer receiving the sample output.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The successful response followed by the message of the final failure.</returns>
    public static async Task<IReadOnlyList<string>> RunAsync(
        TextWriter output,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);

        var chains = new[]
        {
            BuildChain(failuresBeforeSuccess: 2, maxAttempts: 3),
            BuildChain(failuresBeforeSuccess: 5, maxAttempts: 2),
        };

        var results = new List<string>();
        foreach (var chain in chains)
        {
            var description = await DescribeAsync(chain, "import", cancellationToken).ConfigureAwait(false);
            results.Add(description);
            await output.WriteLineAsync($"import => {description}").ConfigureAwait(false);
        }

        return results;
    }
}
