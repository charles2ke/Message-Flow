package io.github.messageflow;

/**
 * Creates the spans emitted by the tracing middleware.
 *
 * <p>The interface is intentionally minimal so the library stays dependency-free: an adapter over
 * OpenTelemetry, or over any other tracing framework, is a few lines of code.
 */
public interface ChainTracer {

    /**
     * Starts a span covering the execution of the remainder of the chain.
     *
     * @param spanName     the name of the span
     * @param requestType  the type of the request flowing through the chain
     * @param responseType the type of the response produced by the chain
     * @return the started span, never {@code null}
     */
    ChainSpan startSpan(String spanName, String requestType, String responseType);
}
