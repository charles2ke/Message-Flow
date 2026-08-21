namespace MessageFlow;

/// <summary>
/// Thrown when no handler of a chain accepted the request and no fallback was configured.
/// </summary>
public sealed class UnhandledRequestException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnhandledRequestException"/> class.
    /// </summary>
    public UnhandledRequestException()
        : base("No handler in the chain handled the request.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnhandledRequestException"/> class.
    /// </summary>
    /// <param name="message">The message describing the error.</param>
    public UnhandledRequestException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnhandledRequestException"/> class.
    /// </summary>
    /// <param name="message">The message describing the error.</param>
    /// <param name="innerException">The exception that caused this error.</param>
    public UnhandledRequestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
