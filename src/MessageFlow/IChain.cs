namespace MessageFlow;

/// <summary>
/// An immutable, pre-compiled chain of responsibility.
/// </summary>
/// <typeparam name="TRequest">The type of the request flowing through the chain.</typeparam>
/// <typeparam name="TResponse">The type of the response produced by the chain.</typeparam>
public interface IChain<TRequest, TResponse>
{
    /// <summary>
    /// Gets the number of handlers in the chain, excluding the terminal fallback.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Sends a request through the chain.
    /// </summary>
    /// <param name="request">The request to process.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The response produced by the first handler that accepted the request.</returns>
    /// <exception cref="UnhandledRequestException">
    /// No handler accepted the request and no fallback was configured.
    /// </exception>
    ValueTask<TResponse> ExecuteAsync(TRequest request, CancellationToken cancellationToken = default);
}
