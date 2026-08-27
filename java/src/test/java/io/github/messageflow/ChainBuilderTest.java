package io.github.messageflow;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertSame;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.CancellationException;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;
import java.util.concurrent.CompletionStage;
import org.junit.jupiter.api.Test;

class ChainBuilderTest {

    private static <T> CompletionStage<T> completed(T value) {
        return CompletableFuture.completedFuture(value);
    }

    private static <T, R> R run(Chain<T, R> chain, T request) {
        return chain.execute(request).toCompletableFuture().join();
    }

    private static Throwable failureOf(Chain<Integer, String> chain, int request) {
        var exception = assertThrows(
                CompletionException.class,
                () -> chain.execute(request).toCompletableFuture().join());
        return exception.getCause();
    }

    @Test
    void firstMatchingHandlerProducesResponse() {
        var chain = Chain.<Integer, String>create()
                .useWhen(request -> request < 0, (request, token) -> completed("negative:" + request))
                .useWhen(request -> request == 0, (request, token) -> completed("zero"))
                .withFallback((request, token) -> completed("positive:" + request))
                .build();

        assertEquals("negative:-7", run(chain, -7));
        assertEquals("zero", run(chain, 0));
        assertEquals("positive:3", run(chain, 3));
        assertEquals(2, chain.count());
    }

    @Test
    void unhandledRequestFailsWhenNoFallbackConfigured() {
        var chain = Chain.<Integer, String>create()
                .useWhen(request -> request < 0, (request, token) -> completed("negative"))
                .build();

        assertInstanceOf(UnhandledRequestException.class, failureOf(chain, 1));
    }

    @Test
    void middlewareRunsAroundTheRestOfTheChain() {
        var log = new ArrayList<String>();

        var chain = Chain.<Integer, String>create()
                .use((request, next, token) -> {
                    log.add("before");
                    return next.handle(request, token).thenApply(response -> {
                        log.add("after");
                        return response + "!";
                    });
                })
                .useWhen(request -> true, (request, token) -> completed("handled"))
                .build();

        assertEquals("handled!", run(chain, 1));
        assertEquals(List.of("before", "after"), log);
    }

    @Test
    void handlerImplementationIsInvoked() {
        var chain = Chain.<Integer, String>create()
                .use(new EvenHandler())
                .withFallback((request, token) -> completed("odd"))
                .build();

        assertEquals("even:2", run(chain, 2));
        assertEquals("odd", run(chain, 3));
    }

    @Test
    void branchIsSkippedWhenPredicateDoesNotMatch() {
        var chain = Chain.<Integer, String>create()
                .useBranch(
                        request -> request > 10,
                        branch -> branch.useWhen(request -> request > 100, (request, token) -> completed("huge")))
                .withFallback((request, token) -> completed("fallback"))
                .build();

        assertEquals("huge", run(chain, 1_000));
        assertEquals("fallback", run(chain, 20));
        assertEquals("fallback", run(chain, 1));
        assertEquals(1, chain.count());
    }

    @Test
    void branchFallbackTerminatesTheChain() {
        var chain = Chain.<Integer, String>create()
                .useBranch(
                        request -> request > 10,
                        branch -> branch.withFallback((request, token) -> completed("branch-fallback")))
                .withFallback((request, token) -> completed("fallback"))
                .build();

        assertEquals("branch-fallback", run(chain, 20));
        assertEquals("fallback", run(chain, 1));
    }

    @Test
    void mergedBuilderFallsThroughToTheParentChain() {
        var fragment = Chain.<Integer, String>create()
                .useWhen(request -> request == 1, (request, token) -> completed("one"));

        var chain = Chain.<Integer, String>create()
                .use(fragment)
                .useWhen(request -> request == 2, (request, token) -> completed("two"))
                .build();

        assertEquals("one", run(chain, 1));
        assertEquals("two", run(chain, 2));
        assertEquals(2, chain.count());

        fragment.useWhen(request -> request == 3, (request, token) -> completed("three"));
        assertInstanceOf(UnhandledRequestException.class, failureOf(chain, 3));
    }

    @Test
    void mergedChainIsRecomposedAgainstTheParentChain() {
        var merged = Chain.<Integer, String>create()
                .useWhen(request -> request == 1, (request, token) -> completed("one"))
                .build();

        var chain = Chain.<Integer, String>create()
                .use(merged)
                .useWhen(request -> request == 2, (request, token) -> completed("two"))
                .build();

        assertEquals("one", run(chain, 1));
        assertEquals("two", run(chain, 2));
        assertInstanceOf(UnhandledRequestException.class, failureOf(chain, 3));
    }

    @Test
    void customChainImplementationTerminatesTheChain() {
        Chain<Integer, String> custom = new Chain<>() {
            @Override
            public int count() {
                return 1;
            }

            @Override
            public CompletionStage<String> execute(Integer request, CancellationToken cancellationToken) {
                return completed("custom:" + request);
            }
        };

        var chain = Chain.<Integer, String>create()
                .use(custom)
                .useWhen(request -> true, (request, token) -> completed("never"))
                .build();

        assertEquals("custom:5", run(chain, 5));
    }

    @Test
    void cancellationTokenFlowsThroughTheChain() {
        var source = new CancellationTokenSource();
        var chain = Chain.<Integer, String>create()
                .useWhen(request -> true, (request, token) -> {
                    token.throwIfCancellationRequested();
                    return completed("handled");
                })
                .build();

        assertEquals("handled", chain.execute(1, source.token()).toCompletableFuture().join());

        source.cancel();
        assertTrue(source.token().isCancellationRequested());
        assertThrows(CancellationException.class, () -> chain.execute(1, source.token()));
        assertFalse(CancellationToken.none().isCancellationRequested());
        CancellationToken.none().throwIfCancellationRequested();
    }

    @Test
    void nullArgumentsAreRejected() {
        var builder = Chain.<Integer, String>create();

        assertThrows(NullPointerException.class, () -> builder.use((Handler<Integer, String>) null));
        assertThrows(NullPointerException.class, () -> builder.use((ChainBuilder<Integer, String>) null));
        assertThrows(NullPointerException.class, () -> builder.use((Chain<Integer, String>) null));
        assertThrows(NullPointerException.class, () -> builder.useWhen(null, (request, token) -> completed("x")));
        assertThrows(NullPointerException.class, () -> builder.useWhen(request -> true, null));
        assertThrows(NullPointerException.class, () -> builder.useBranch(null, branch -> { }));
        assertThrows(NullPointerException.class, () -> builder.useBranch(request -> true, null));
        assertThrows(NullPointerException.class, () -> builder.withFallback(null));
    }

    @Test
    void emptyChainWithFallbackIsSupported() {
        var chain = Chain.<Integer, String>create()
                .withFallback((request, token) -> completed("fallback"))
                .build();

        assertEquals(0, chain.count());
        assertEquals("fallback", run(chain, 1));
    }

    @Test
    void createReturnsANewBuilderEveryTime() {
        var first = Chain.<Integer, String>create();
        var second = Chain.<Integer, String>create();

        assertNotNull(first);
        assertNotNull(second);
        assertFalse(first == second);
    }

    @Test
    void executeUsesTheNonCancellableTokenByDefault() {
        var captured = new CancellationToken[1];
        var chain = Chain.<Integer, String>create()
                .useWhen(request -> true, (request, token) -> {
                    captured[0] = token;
                    return completed("handled");
                })
                .build();

        assertEquals("handled", run(chain, 1));
        assertSame(CancellationToken.none(), captured[0]);
    }

    private static final class EvenHandler extends HandlerBase<Integer, String> {

        @Override
        protected boolean canHandle(Integer request) {
            return request % 2 == 0;
        }

        @Override
        protected CompletionStage<String> process(Integer request, CancellationToken cancellationToken) {
            return completed("even:" + request);
        }
    }
}
