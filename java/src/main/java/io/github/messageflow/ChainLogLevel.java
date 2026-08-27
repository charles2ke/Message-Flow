package io.github.messageflow;

/**
 * The severity of an entry written by a chain to a {@link ChainLogger}.
 */
public enum ChainLogLevel {

    /** The most verbose level, used for step-by-step diagnostics. */
    TRACE,

    /** Diagnostic information useful while developing. */
    DEBUG,

    /** The normal flow of the chain. */
    INFORMATION,

    /** An abnormal but recoverable situation. */
    WARNING,

    /** A request that failed with an exception. */
    ERROR
}
