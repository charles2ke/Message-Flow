namespace MessageFlow;

/// <summary>
/// Receives the log entries a chain writes while executing a request.
/// </summary>
/// <remarks>
/// The interface is intentionally minimal so the library stays dependency-free: an adapter over
/// <c>Microsoft.Extensions.Logging.ILogger</c>, or over any other logging framework, is a few lines
/// of code.
/// </remarks>
public interface IChainLogger
{
    /// <summary>
    /// Determines whether entries of the given level are recorded.
    /// </summary>
    /// <param name="level">The level to check.</param>
    /// <returns><see langword="true"/> when entries of <paramref name="level"/> are recorded.</returns>
    bool IsEnabled(ChainLogLevel level);

    /// <summary>
    /// Records a log entry.
    /// </summary>
    /// <param name="level">The severity of the entry.</param>
    /// <param name="message">The message describing what the chain did.</param>
    /// <param name="exception">The exception that failed the request, if any.</param>
    void Log(ChainLogLevel level, string message, Exception? exception);
}
