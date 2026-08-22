namespace MessageFlow;

/// <summary>
/// Builds an immutable <see cref="IChain{TRequest, TResponse}"/> from an ordered set of handlers.
/// </summary>
/// <typeparam name="TRequest">The type of the request flowing through the chain.</typeparam>
/// <typeparam name="TResponse">The type of the response produced by the chain.</typeparam>
public sealed class ChainBuilder<TRequest, TResponse>
{
    private readonly List<Func<NextHandler<TRequest, TResponse>, NextHandler<TRequest, TResponse>>> _steps = [];
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

        _steps.Add(nextHandler => (request, cancellationToken) => handler.HandleAsync(request, nextHandler, cancellationToken));
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
    /// Appends a nested sub-chain that only runs when <paramref name="predicate"/> matches the request.
    /// </summary>
    /// <remarks>
    /// When the predicate does not match, the request skips the branch entirely and flows to the next
    /// handler of the parent chain. When it does match but no handler of the branch accepts the request,
    /// the request falls through to the next handler of the parent chain as well — unless the branch
    /// configures its own fallback, which then becomes the terminal step of the branch.
    /// The branch is configured immediately and composed at <see cref="Build"/> time, so it costs a
    /// single extra delegate call per request. It counts as one handler towards
    /// <see cref="IChain{TRequest, TResponse}.Count"/>, regardless of how many handlers it contains.
    /// </remarks>
    /// <param name="predicate">Decides whether the request enters the branch.</param>
    /// <param name="configure">Adds the handlers of the branch to the supplied builder.</param>
    /// <returns>The same builder instance, for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="predicate"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    public ChainBuilder<TRequest, TResponse> UseBranch(
        Func<TRequest, bool> predicate,
        Action<ChainBuilder<TRequest, TResponse>> configure)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(configure);

        var branch = new ChainBuilder<TRequest, TResponse>();
        configure(branch);

        _steps.Add(nextHandler =>
        {
            var branchPipeline = branch.BuildPipeline(nextHandler);
            return (request, cancellationToken) => predicate(request)
                ? branchPipeline(request, cancellationToken)
                : nextHandler(request, cancellationToken);
        });

        return this;
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
        var pipeline = BuildPipeline(static (_, _) => throw new UnhandledRequestException());

        return new Chain<TRequest, TResponse>(pipeline, _steps.Count);
    }

    /// <summary>
    /// Composes the configured handlers into a single delegate, using <paramref name="terminal"/> as the
    /// step invoked when no handler accepted the request and no fallback was configured.
    /// </summary>
    /// <param name="terminal">The step invoked when the configured handlers do not accept the request.</param>
    /// <returns>The composed pipeline.</returns>
    internal NextHandler<TRequest, TResponse> BuildPipeline(NextHandler<TRequest, TResponse> terminal)
    {
        var fallback = _fallback;
        var pipeline = fallback is null
            ? terminal
            : (request, cancellationToken) => fallback(request, cancellationToken);

        var steps = _steps.ToArray();
        for (var i = steps.Length - 1; i >= 0; i--)
        {
            pipeline = steps[i](pipeline);
        }

        return pipeline;
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
