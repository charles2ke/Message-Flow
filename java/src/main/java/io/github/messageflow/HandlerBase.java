package io.github.messageflow;

import java.util.Objects;
import java.util.concurrent.CompletionStage;

/**
 * Convenience base class for handlers that either fully handle a request or pass it on.
 *
 * @param <T> the type of the request flowing through the chain
 * @param <R> the type of the response produced by the chain
 */
public abstract class HandlerBase<T, R> implements Handler<T, R> {

    /**
     * Determines whether this handler is responsible for the given request.
     *
     * @param request the request to inspect
     * @return {@code true} when this handler should process the request
     */
    protected abstract boolean canHandle(T request);

    /**
     * Processes a request this handler is responsible for.
     *
     * @param request           the request to process
     * @param cancellationToken a token used to cancel the operation
     * @return the response for the request
     */
    protected abstract CompletionStage<R> process(T request, CancellationToken cancellationToken);

    @Override
    public final CompletionStage<R> handle(
            T request,
            NextHandler<T, R> nextHandler,
            CancellationToken cancellationToken) {
        Objects.requireNonNull(nextHandler, "nextHandler");

        return canHandle(request)
                ? process(request, cancellationToken)
                : nextHandler.handle(request, cancellationToken);
    }
}
