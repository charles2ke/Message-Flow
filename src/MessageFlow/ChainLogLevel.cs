namespace MessageFlow;

/// <summary>
/// The severity of an entry written by a chain to an <see cref="IChainLogger"/>.
/// </summary>
public enum ChainLogLevel
{
    /// <summary>The most verbose level, used for step-by-step diagnostics.</summary>
    Trace,

    /// <summary>Diagnostic information useful while developing.</summary>
    Debug,

    /// <summary>The normal flow of the chain.</summary>
    Information,

    /// <summary>An abnormal but recoverable situation.</summary>
    Warning,

    /// <summary>A request that failed with an exception.</summary>
    Error,
}
