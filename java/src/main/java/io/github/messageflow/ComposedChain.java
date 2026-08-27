package io.github.messageflow;

import java.util.Objects;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionStage;
import java.util.function.UnaryOperator;

/**
 * Default {@link Chain} implementation. The handler pipeline is composed once, at build time, so
 * execution is a simple delegate call.
 *
 * @param <T> the type of the request flowing through the chain
 * @param <R> the type of the response produced by the chain
 */
public final class ComposedChain<T, R> implements Chain<T, R> {

    private final UnaryOperator<NextHandler<T, R>> composer;
    private final NextHandler<T, R> pipeline;
    private final int count;

    ComposedChain(UnaryOperator<NextHandler<T, R>> composer, int count) {
        this.composer = composer;
        this.count = count;
        this.pipeline = composer.apply(ComposedChain::unhandled);
    }

    /**
     * Gets the open composition of the chain: it turns the step invoked when no handler of this chain
     * accepted the request into the executable pipeline. It allows the chain to be merged into another
     * chain without re-running its builder.
     *
     * @return the open composition of the chain
     */
    UnaryOperator<NextHandler<T, R>> composer() {
        return composer;
    }

    @Override
    public int count() {
        return count;
    }

    @Override
    public CompletionStage<R> execute(T request, CancellationToken cancellationToken) {
        Objects.requireNonNull(cancellationToken, "cancellationToken");
        return pipeline.handle(request, cancellationToken);
    }

    private static <T, R> CompletionStage<R> unhandled(T request, CancellationToken cancellationToken) {
        return CompletableFuture.failedFuture(new UnhandledRequestException());
    }
}
