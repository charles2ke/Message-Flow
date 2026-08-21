namespace MessageFlow;

/// <summary>
/// Default <see cref="IChain{TRequest, TResponse}"/> implementation.
/// The handler pipeline is composed once, at build time, so execution is a simple delegate call.
/// </summary>
/// <typeparam name="TRequest">The type of the request flowing through the chain.</typeparam>
/// <typeparam name="TResponse">The type of the response produced by the chain.</typeparam>
public sealed class Chain<TRequest, TResponse> : IChain<TRequest, TResponse>
{
    private readonly NextHandler<TRequest, TResponse> _pipeline;

    internal Chain(NextHandler<TRequest, TResponse> pipeline, int count)
    {
        _pipeline = pipeline;
        Count = count;
    }

    /// <inheritdoc />
    public int Count { get; }

    /// <inheritdoc />
    public ValueTask<TResponse> ExecuteAsync(TRequest request, CancellationToken cancellationToken = default)
        => _pipeline(request, cancellationToken);
}
