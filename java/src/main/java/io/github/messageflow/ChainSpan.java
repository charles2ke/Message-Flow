package io.github.messageflow;

/**
 * A unit of tracing work covering the execution of the remainder of a chain.
 */
public interface ChainSpan extends AutoCloseable {

    /** Marks the span as successfully completed. */
    void setOk();

    /**
     * Marks the span as failed.
     *
     * @param throwable the exception that failed the request
     */
    void setError(Throwable throwable);

    /** Ends the span. */
    @Override
    void close();
}
