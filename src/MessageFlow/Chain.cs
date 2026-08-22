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

    internal Chain(
        Func<NextHandler<TRequest, TResponse>, NextHandler<TRequest, TResponse>> composer,
        int count)
    {
        Composer = composer;
        Count = count;
        _pipeline = composer(static (_, _) => throw new UnhandledRequestException());
    }

    /// <summary>
    /// Gets the open composition of the chain: it turns the step invoked when no handler of this
    /// chain accepted the request into the executable pipeline. It allows the chain to be merged
    /// into another chain without re-running its builder.
    /// </summary>
    internal Func<NextHandler<TRequest, TResponse>, NextHandler<TRequest, TResponse>> Composer { get; }

    /// <inheritdoc />
    public int Count { get; }

    /// <inheritdoc />
    public ValueTask<TResponse> ExecuteAsync(TRequest request, CancellationToken cancellationToken = default)
        => _pipeline(request, cancellationToken);
}
