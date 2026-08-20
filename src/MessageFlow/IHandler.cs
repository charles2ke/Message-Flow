namespace MessageFlow;

/// <summary>
/// A single link of a chain of responsibility.
/// </summary>
/// <typeparam name="TRequest">The type of the request flowing through the chain.</typeparam>
/// <typeparam name="TResponse">The type of the response produced by the chain.</typeparam>
public interface IHandler<TRequest, TResponse>
{
    /// <summary>
    /// Processes the request, optionally delegating to the next handler of the chain.
    /// </summary>
    /// <param name="request">The request to process.</param>
    /// <param name="nextHandler">The next handler of the chain.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The response for the request.</returns>
    ValueTask<TResponse> HandleAsync(
        TRequest request,
        NextHandler<TRequest, TResponse> nextHandler,
        CancellationToken cancellationToken);
}
