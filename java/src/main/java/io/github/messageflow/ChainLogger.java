package io.github.messageflow;

/**
 * Receives the log entries a chain writes while executing a request.
 *
 * <p>The interface is intentionally minimal so the library stays dependency-free: an adapter over
 * SLF4J, {@code java.util.logging} or any other logging framework is a few lines of code.
 */
public interface ChainLogger {

    /**
     * Determines whether entries of the given level are recorded.
     *
     * @param level the level to check
     * @return {@code true} when entries of {@code level} are recorded
     */
    boolean isEnabled(ChainLogLevel level);

    /**
     * Records a log entry.
     *
     * @param level     the severity of the entry
     * @param message   the message describing what the chain did
     * @param throwable the exception that failed the request, or {@code null}
     */
    void log(ChainLogLevel level, String message, Throwable throwable);
}
