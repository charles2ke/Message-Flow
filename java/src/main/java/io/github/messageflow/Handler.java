package io.github.messageflow;

import java.util.concurrent.CompletionStage;

/**
 * A single link of a chain of responsibility.
 *
 * @param <T> the type of the request flowing through the chain
 * @param <R> the type of the response produced by the chain
 */
@FunctionalInterface
public interface Handler<T, R> {

    /**
     * Processes the request, optionally delegating to the next handler of the chain.
     *
     * @param request           the request to process
     * @param nextHandler       the next handler of the chain
     * @param cancellationToken a token used to cancel the operation
     * @return the response for the request
     */
    CompletionStage<R> handle(T request, NextHandler<T, R> nextHandler, CancellationToken cancellationToken);
}
