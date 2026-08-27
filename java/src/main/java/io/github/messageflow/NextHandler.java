package io.github.messageflow;

import java.util.concurrent.CompletionStage;

/**
 * Represents the next step of a chain of responsibility.
 *
 * @param <T> the type of the request flowing through the chain
 * @param <R> the type of the response produced by the chain
 */
@FunctionalInterface
public interface NextHandler<T, R> {

    /**
     * Processes the request with the remainder of the chain.
     *
     * @param request           the request to process
     * @param cancellationToken a token used to cancel the operation
     * @return the response produced by the remainder of the chain
     */
    CompletionStage<R> handle(T request, CancellationToken cancellationToken);
}
