package io.github.messageflow;

import java.util.ArrayList;
import java.util.List;
import java.util.Locale;
import java.util.Objects;
import java.util.concurrent.CompletionException;
import java.util.concurrent.CompletionStage;
import java.util.function.Consumer;
import java.util.function.Predicate;
import java.util.function.UnaryOperator;

/**
 * Builds an immutable {@link Chain} from an ordered set of handlers.
 *
 * @param <T> the type of the request flowing through the chain
 * @param <R> the type of the response produced by the chain
 */
public final class ChainBuilder<T, R> {

    private final List<UnaryOperator<NextHandler<T, R>>> steps = new ArrayList<>();
    private NextHandler<T, R> fallback;

    ChainBuilder() {
    }

    /**
     * Appends a handler to the end of the chain.
     *
     * @param handler the handler to append
     * @return the same builder instance, for chaining
     * @throws NullPointerException {@code handler} is {@code null}
     */
    public ChainBuilder<T, R> use(Handler<T, R> handler) {
        Objects.requireNonNull(handler, "handler");

        steps.add(nextHandler -> (request, cancellationToken) ->
                handler.handle(request, nextHandler, cancellationToken));
        return this;
    }

    /**
     * Appends every handler of {@code other} to the end of the chain.
     *
     * <p>The merged handlers are composed against the continuation of this chain, so a request the
     * merged handlers do not accept flows to the next handler of this chain — unless {@code other}
     * configures its own fallback, which then becomes the terminal step of the merged segment and the
     * remaining handlers of this chain are never reached. The handlers of {@code other} are
     * snapshotted at merge time, so later changes to {@code other} do not affect this chain, and
     * merging a builder into itself is safe. The merged segment counts as one handler towards
     * {@link Chain#count()}, regardless of how many handlers it contains.
     *
     * @param other the builder whose handlers are appended
     * @return the same builder instance, for chaining
     * @throws NullPointerException {@code other} is {@code null}
     */
    public ChainBuilder<T, R> use(ChainBuilder<T, R> other) {
        Objects.requireNonNull(other, "other");

        steps.add(other.createComposer());
        return this;
    }

    /**
     * Appends an already built chain to the end of this chain.
     *
     * <p>Chains built by {@link #build()} are re-composed against the continuation of this chain, so a
     * request the merged chain does not accept flows to the next handler of this chain instead of
     * failing with {@link UnhandledRequestException} — unless the merged chain was built with a
     * fallback, which then becomes the terminal step of the merged segment. A custom {@link Chain}
     * implementation cannot be re-composed, so it is executed as-is and terminates the chain. The
     * merged chain counts as one handler towards {@link Chain#count()}, regardless of how many
     * handlers it contains.
     *
     * @param chain the chain to append
     * @return the same builder instance, for chaining
     * @throws NullPointerException {@code chain} is {@code null}
     */
    public ChainBuilder<T, R> use(Chain<T, R> chain) {
        Objects.requireNonNull(chain, "chain");

        if (chain instanceof ComposedChain<T, R> composed) {
            steps.add(composed.composer());
        } else {
            steps.add(ignoredNextHandler -> chain::execute);
        }

        return this;
    }

    /**
     * Appends a handler that only runs when {@code predicate} matches the request; otherwise the
     * request flows to the next handler.
     *
     * @param predicate decides whether the handler is responsible for the request
     * @param handler   produces the response for accepted requests
     * @return the same builder instance, for chaining
     * @throws NullPointerException {@code predicate} or {@code handler} is {@code null}
     */
    public ChainBuilder<T, R> useWhen(Predicate<T> predicate, NextHandler<T, R> handler) {
        Objects.requireNonNull(predicate, "predicate");
        Objects.requireNonNull(handler, "handler");

        return use((request, nextHandler, cancellationToken) -> predicate.test(request)
                ? handler.handle(request, cancellationToken)
                : nextHandler.handle(request, cancellationToken));
    }

    /**
     * Appends a nested sub-chain that only runs when {@code predicate} matches the request.
     *
     * <p>When the predicate does not match, the request skips the branch entirely and flows to the
     * next handler of the parent chain. When it does match but no handler of the branch accepts the
     * request, the request falls through to the next handler of the parent chain as well — unless the
     * branch configures its own fallback, which then becomes the terminal step of the branch. The
     * branch is configured immediately and composed at {@link #build()} time, so it costs a single
     * extra call per request. It counts as one handler towards {@link Chain#count()}, regardless of
     * how many handlers it contains.
     *
     * @param predicate decides whether the request enters the branch
     * @param configure adds the handlers of the branch to the supplied builder
     * @return the same builder instance, for chaining
     * @throws NullPointerException {@code predicate} or {@code configure} is {@code null}
     */
    public ChainBuilder<T, R> useBranch(Predicate<T> predicate, Consumer<ChainBuilder<T, R>> configure) {
        Objects.requireNonNull(predicate, "predicate");
        Objects.requireNonNull(configure, "configure");

        var branch = new ChainBuilder<T, R>();
        configure.accept(branch);
        var branchComposer = branch.createComposer();

        steps.add(nextHandler -> {
            var branchPipeline = branchComposer.apply(nextHandler);
            return (request, cancellationToken) -> predicate.test(request)
                    ? branchPipeline.handle(request, cancellationToken)
                    : nextHandler.handle(request, cancellationToken);
        });

        return this;
    }

    /**
     * Appends a middleware that logs the start, the completion and the failure of every request
     * flowing through the remainder of the chain.
     *
     * <p>Only the chain name and durations are logged; the request itself is never written to the log,
     * so no payload can leak into log storage. Failures are logged at {@link ChainLogLevel#ERROR} and
     * the exception is propagated unchanged.
     *
     * @param logger    the logger receiving the entries
     * @param level     the level used for the start and completion entries
     * @param chainName the name identifying the chain in the log entries
     * @return the same builder instance, for chaining
     * @throws NullPointerException any argument is {@code null}
     */
    public ChainBuilder<T, R> useLogging(ChainLogger logger, ChainLogLevel level, String chainName) {
        Objects.requireNonNull(logger, "logger");
        Objects.requireNonNull(level, "level");
        Objects.requireNonNull(chainName, "chainName");

        return use((request, nextHandler, cancellationToken) -> {
            if (logger.isEnabled(level)) {
                logger.log(level, "Executing chain " + chainName + ".", null);
            }

            var timestamp = System.nanoTime();

            CompletionStage<R> stage;
            try {
                stage = nextHandler.handle(request, cancellationToken);
            } catch (RuntimeException exception) {
                logFailure(logger, chainName, timestamp, exception);
                throw exception;
            }

            return stage.whenComplete((response, throwable) -> {
                if (throwable == null) {
                    if (logger.isEnabled(level)) {
                        logger.log(level, "Executed chain " + chainName + " in " + elapsed(timestamp) + " ms.", null);
                    }
                } else {
                    logFailure(logger, chainName, timestamp, unwrap(throwable));
                }
            });
        });
    }

    /**
     * Appends a logging middleware named after the library, using {@link ChainLogLevel#DEBUG}.
     *
     * @param logger the logger receiving the entries
     * @return the same builder instance, for chaining
     * @throws NullPointerException {@code logger} is {@code null}
     */
    public ChainBuilder<T, R> useLogging(ChainLogger logger) {
        return useLogging(logger, ChainLogLevel.DEBUG, ChainDiagnostics.TRACER_NAME);
    }

    /**
     * Appends a middleware that wraps the remainder of the chain in a {@link ChainSpan}.
     *
     * <p>Failures mark the span as failed and propagate the exception unchanged. The span is always
     * ended, whether the request succeeded or not.
     *
     * @param tracer       the tracer creating the spans
     * @param spanName     the name of the created span
     * @param requestType  the request type reported on the span, may be {@code null}
     * @param responseType the response type reported on the span, may be {@code null}
     * @return the same builder instance, for chaining
     * @throws NullPointerException {@code tracer} or {@code spanName} is {@code null}
     * @throws IllegalArgumentException {@code spanName} is empty
     */
    public ChainBuilder<T, R> useTracing(
            ChainTracer tracer,
            String spanName,
            String requestType,
            String responseType) {
        Objects.requireNonNull(tracer, "tracer");
        Objects.requireNonNull(spanName, "spanName");
        if (spanName.isEmpty()) {
            throw new IllegalArgumentException("spanName must not be empty.");
        }

        return use((request, nextHandler, cancellationToken) -> {
            var span = Objects.requireNonNull(
                    tracer.startSpan(spanName, requestType, responseType),
                    "ChainTracer.startSpan must not return null");

            CompletionStage<R> stage;
            try {
                stage = nextHandler.handle(request, cancellationToken);
            } catch (RuntimeException exception) {
                span.setError(exception);
                span.close();
                throw exception;
            }

            return stage.whenComplete((response, throwable) -> {
                if (throwable == null) {
                    span.setOk();
                } else {
                    span.setError(unwrap(throwable));
                }

                span.close();
            });
        });
    }

    /**
     * Appends a tracing middleware using the default span name and the given types.
     *
     * @param tracer       the tracer creating the spans
     * @param requestType  the request type reported on the span
     * @param responseType the response type reported on the span
     * @return the same builder instance, for chaining
     * @throws NullPointerException any argument is {@code null}
     */
    public ChainBuilder<T, R> useTracing(ChainTracer tracer, Class<T> requestType, Class<R> responseType) {
        Objects.requireNonNull(requestType, "requestType");
        Objects.requireNonNull(responseType, "responseType");

        return useTracing(tracer, ChainDiagnostics.EXECUTE_SPAN_NAME, requestType.getName(), responseType.getName());
    }

    /**
     * Sets the terminal step invoked when no handler accepted the request. Without a fallback the
     * chain fails with {@link UnhandledRequestException} instead.
     *
     * @param fallback the terminal handler
     * @return the same builder instance, for chaining
     * @throws NullPointerException {@code fallback} is {@code null}
     */
    public ChainBuilder<T, R> withFallback(NextHandler<T, R> fallback) {
        Objects.requireNonNull(fallback, "fallback");

        this.fallback = fallback;
        return this;
    }

    /**
     * Composes the configured handlers into an immutable chain.
     *
     * @return the composed chain
     */
    public Chain<T, R> build() {
        return new ComposedChain<>(createComposer(), steps.size());
    }

    /**
     * Snapshots the configured handlers into an open composition: a function turning the step invoked
     * when no handler accepted the request into the composed pipeline.
     *
     * @return the open composition of the configured handlers
     */
    private UnaryOperator<NextHandler<T, R>> createComposer() {
        List<UnaryOperator<NextHandler<T, R>>> snapshot = List.copyOf(steps);
        var snapshotFallback = fallback;

        return terminal -> {
            NextHandler<T, R> pipeline = snapshotFallback == null ? terminal : snapshotFallback;

            for (var i = snapshot.size() - 1; i >= 0; i--) {
                pipeline = snapshot.get(i).apply(pipeline);
            }

            return pipeline;
        };
    }

    private static void logFailure(ChainLogger logger, String chainName, long timestamp, Throwable throwable) {
        if (logger.isEnabled(ChainLogLevel.ERROR)) {
            logger.log(
                    ChainLogLevel.ERROR,
                    "Chain " + chainName + " failed after " + elapsed(timestamp) + " ms.",
                    throwable);
        }
    }

    private static Throwable unwrap(Throwable throwable) {
        return throwable instanceof CompletionException && throwable.getCause() != null
                ? throwable.getCause()
                : throwable;
    }

    private static String elapsed(long timestamp) {
        var elapsedMilliseconds = (System.nanoTime() - timestamp) / 1_000_000d;
        return String.format(Locale.ROOT, "%.3f", elapsedMilliseconds);
    }
}
