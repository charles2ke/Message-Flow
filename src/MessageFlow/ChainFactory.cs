namespace MessageFlow;

/// <summary>
/// Entry point for creating chains of responsibility.
/// </summary>
public static class Chain
{
    /// <summary>
    /// Creates a builder for a chain that turns a <typeparamref name="TRequest"/> into a
    /// <typeparamref name="TResponse"/>.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request flowing through the chain.</typeparam>
    /// <typeparam name="TResponse">The type of the response produced by the chain.</typeparam>
    /// <returns>A new, empty builder.</returns>
    public static ChainBuilder<TRequest, TResponse> Create<TRequest, TResponse>() => new();
}
