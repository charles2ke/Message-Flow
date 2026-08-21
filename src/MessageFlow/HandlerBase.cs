namespace MessageFlow;

/// <summary>
/// Convenience base class for handlers that either fully handle a request or pass it on.
/// </summary>
/// <typeparam name="TRequest">The type of the request flowing through the chain.</typeparam>
/// <typeparam name="TResponse">The type of the response produced by the chain.</typeparam>
public abstract class HandlerBase<TRequest, TResponse> : IHandler<TRequest, TResponse>
{
    /// <summary>
    /// Determines whether this handler is responsible for the given request.
    /// </summary>
    /// <param name="request">The request to inspect.</param>
    /// <returns><see langword="true"/> when this handler should process the request.</returns>
    protected abstract bool CanHandle(TRequest request);

    /// <summary>
    /// Processes a request this handler is responsible for.
    /// </summary>
    /// <param name="request">The request to process.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The response for the request.</returns>
    protected abstract ValueTask<TResponse> ProcessAsync(TRequest request, CancellationToken cancellationToken);

    /// <inheritdoc />
    public ValueTask<TResponse> HandleAsync(
        TRequest request,
        NextHandler<TRequest, TResponse> nextHandler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nextHandler);

        return CanHandle(request)
            ? ProcessAsync(request, cancellationToken)
            : nextHandler(request, cancellationToken);
    }
}
