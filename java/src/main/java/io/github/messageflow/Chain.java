package io.github.messageflow;

import java.util.concurrent.CompletionStage;

/**
 * An immutable, pre-compiled chain of responsibility.
 *
 * @param <T> the type of the request flowing through the chain
 * @param <R> the type of the response produced by the chain
 */
public interface Chain<T, R> {

    /**
     * Creates a builder for a chain that turns a request into a response.
     *
     * @param <T> the type of the request flowing through the chain
     * @param <R> the type of the response produced by the chain
     * @return a new, empty builder
     */
    static <T, R> ChainBuilder<T, R> create() {
        return new ChainBuilder<>();
    }

    /**
     * Gets the number of handlers in the chain, excluding the terminal fallback.
     *
     * @return the number of handlers in the chain
     */
    int count();

    /**
     * Sends a request through the chain.
     *
     * <p>When no handler accepts the request and no fallback was configured, the returned stage
     * completes exceptionally with an {@link UnhandledRequestException}.
     *
     * @param request           the request to process
     * @param cancellationToken a token used to cancel the operation
     * @return the response produced by the first handler that accepted the request
     */
    CompletionStage<R> execute(T request, CancellationToken cancellationToken);

    /**
     * Sends a request through the chain without cancellation support.
     *
     * @param request the request to process
     * @return the response produced by the first handler that accepted the request
     */
    default CompletionStage<R> execute(T request) {
        return execute(request, CancellationToken.none());
    }
}
