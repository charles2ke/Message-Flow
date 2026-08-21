namespace MessageFlow;

/// <summary>
/// Builds an immutable <see cref="IChain{TRequest, TResponse}"/> from an ordered set of handlers.
/// </summary>
/// <typeparam name="TRequest">The type of the request flowing through the chain.</typeparam>
/// <typeparam name="TResponse">The type of the response produced by the chain.</typeparam>
public sealed class ChainBuilder<TRequest, TResponse>
{
    private readonly List<IHandler<TRequest, TResponse>> _handlers = [];
    private Func<TRequest, CancellationToken, ValueTask<TResponse>>? _fallback;

    /// <summary>
    /// Appends a handler to the end of the chain.
    /// </summary>
    /// <param name="handler">The handler to append.</param>
    /// <returns>The same builder instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
    public ChainBuilder<TRequest, TResponse> Use(IHandler<TRequest, TResponse> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        _handlers.Add(handler);
        return this;
    }

    /// <summary>
    /// Appends an inline handler to the end of the chain.
    /// </summary>
    /// <param name="handler">
    /// The handler implementation. It receives the request, the next step of the chain and a cancellation token.
    /// </param>
    /// <returns>The same builder instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
    public ChainBuilder<TRequest, TResponse> Use(
        Func<TRequest, NextHandler<TRequest, TResponse>, CancellationToken, ValueTask<TResponse>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return Use(new DelegateHandler(handler));
    }

    /// <summary>
    /// Appends a handler that only runs when <paramref name="predicate"/> matches the request;
    /// otherwise the request flows to the next handler.
    /// </summary>
    /// <param name="predicate">Decides whether the handler is responsible for the request.</param>
    /// <param name="handler">Produces the response for accepted requests.</param>
    /// <returns>The same builder instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="predicate"/> or <paramref name="handler"/> is <see langword="null"/>.
    /// </exception>
    public ChainBuilder<TRequest, TResponse> UseWhen(
        Func<TRequest, bool> predicate,
        Func<TRequest, CancellationToken, ValueTask<TResponse>> handler)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(handler);

        return Use(new PredicateHandler(predicate, handler));
    }

    /// <summary>
    /// Sets the terminal step invoked when no handler accepted the request.
    /// Without a fallback the chain throws <see cref="UnhandledRequestException"/> instead.
    /// </summary>
    /// <param name="fallback">The terminal handler.</param>
    /// <returns>The same builder instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fallback"/> is <see langword="null"/>.</exception>
    public ChainBuilder<TRequest, TResponse> WithFallback(
        Func<TRequest, CancellationToken, ValueTask<TResponse>> fallback)
    {
        ArgumentNullException.ThrowIfNull(fallback);

        _fallback = fallback;
        return this;
    }

    /// <summary>
    /// Composes the configured handlers into an immutable chain.
    /// </summary>
    /// <returns>The composed chain.</returns>
    public IChain<TRequest, TResponse> Build()
    {
        var fallback = _fallback;
        NextHandler<TRequest, TResponse> pipeline = fallback is null
            ? static (_, _) => throw new UnhandledRequestException()
            : (request, cancellationToken) => fallback(request, cancellationToken);

        var handlers = _handlers.ToArray();
        for (var i = handlers.Length - 1; i >= 0; i--)
        {
            var handler = handlers[i];
            var nextHandler = pipeline;
            pipeline = (request, cancellationToken) => handler.HandleAsync(request, nextHandler, cancellationToken);
        }

        return new Chain<TRequest, TResponse>(pipeline, handlers.Length);
    }

    private sealed class DelegateHandler(
        Func<TRequest, NextHandler<TRequest, TResponse>, CancellationToken, ValueTask<TResponse>> handler)
        : IHandler<TRequest, TResponse>
    {
        public ValueTask<TResponse> HandleAsync(
            TRequest request,
            NextHandler<TRequest, TResponse> nextHandler,
            CancellationToken cancellationToken)
            => handler(request, nextHandler, cancellationToken);
    }

    private sealed class PredicateHandler(
        Func<TRequest, bool> predicate,
        Func<TRequest, CancellationToken, ValueTask<TResponse>> handler)
        : IHandler<TRequest, TResponse>
    {
        public ValueTask<TResponse> HandleAsync(
            TRequest request,
            NextHandler<TRequest, TResponse> nextHandler,
            CancellationToken cancellationToken)
            => predicate(request)
                ? handler(request, cancellationToken)
                : nextHandler(request, cancellationToken);
    }
}
