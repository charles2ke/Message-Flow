namespace MessageFlow;

/// <summary>
/// Represents the next step of a chain of responsibility.
/// </summary>
/// <typeparam name="TRequest">The type of the request flowing through the chain.</typeparam>
/// <typeparam name="TResponse">The type of the response produced by the chain.</typeparam>
/// <param name="request">The request to process.</param>
/// <param name="cancellationToken">A token used to cancel the operation.</param>
/// <returns>The response produced by the remainder of the chain.</returns>
public delegate ValueTask<TResponse> NextHandler<TRequest, TResponse>(
    TRequest request,
    CancellationToken cancellationToken);
