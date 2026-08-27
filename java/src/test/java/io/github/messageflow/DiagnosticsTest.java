package io.github.messageflow;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;
import java.util.concurrent.CompletionStage;
import org.junit.jupiter.api.Test;

class DiagnosticsTest {

    private static <T> CompletionStage<T> completed(T value) {
        return CompletableFuture.completedFuture(value);
    }

    @Test
    void loggingMiddlewareRecordsStartAndCompletion() {
        var logger = new RecordingLogger(true);

        var chain = Chain.<Integer, String>create()
                .useLogging(logger, ChainLogLevel.INFORMATION, "orders")
                .useWhen(request -> true, (request, token) -> completed("handled"))
                .build();

        assertEquals("handled", chain.execute(1).toCompletableFuture().join());
        assertEquals(2, logger.entries.size());
        assertTrue(logger.entries.get(0).message.startsWith("Executing chain orders."));
        assertTrue(logger.entries.get(1).message.startsWith("Executed chain orders in "));
        assertEquals(ChainLogLevel.INFORMATION, logger.entries.get(1).level);
        assertNull(logger.entries.get(1).throwable);
    }

    @Test
    void loggingMiddlewareRecordsAsynchronousFailures() {
        var logger = new RecordingLogger(true);
        var failure = new IllegalArgumentException("boom");

        var chain = Chain.<Integer, String>create()
                .useLogging(logger)
                .useWhen(request -> true, (request, token) -> CompletableFuture.failedFuture(failure))
                .build();

        var thrown = assertThrows(
                CompletionException.class,
                () -> chain.execute(1).toCompletableFuture().join());

        assertEquals(failure, thrown.getCause());
        var last = logger.entries.get(logger.entries.size() - 1);
        assertEquals(ChainLogLevel.ERROR, last.level);
        assertEquals(failure, last.throwable);
        assertTrue(last.message.startsWith("Chain MessageFlow failed after "));
    }

    @Test
    void loggingMiddlewareRecordsSynchronousFailures() {
        var logger = new RecordingLogger(true);

        var chain = Chain.<Integer, String>create()
                .useLogging(logger)
                .use((request, next, token) -> {
                    throw new IllegalStateException("sync boom");
                })
                .build();

        var thrown = assertThrows(IllegalStateException.class, () -> chain.execute(1));

        assertEquals("sync boom", thrown.getMessage());
        var last = logger.entries.get(logger.entries.size() - 1);
        assertEquals(ChainLogLevel.ERROR, last.level);
        assertEquals(thrown, last.throwable);
    }

    @Test
    void disabledLoggerReceivesNoEntries() {
        var logger = new RecordingLogger(false);

        var chain = Chain.<Integer, String>create()
                .useLogging(logger)
                .useWhen(request -> true, (request, token) -> completed("handled"))
                .build();

        assertEquals("handled", chain.execute(1).toCompletableFuture().join());
        assertTrue(logger.entries.isEmpty());
    }

    @Test
    void loggingRejectsNullArguments() {
        var builder = Chain.<Integer, String>create();

        assertThrows(NullPointerException.class, () -> builder.useLogging(null));
        assertThrows(NullPointerException.class, () -> builder.useLogging(new RecordingLogger(true), null, "name"));
        assertThrows(
                NullPointerException.class,
                () -> builder.useLogging(new RecordingLogger(true), ChainLogLevel.DEBUG, null));
    }

    @Test
    void tracingMiddlewareCompletesTheSpan() {
        var tracer = new RecordingTracer();

        var chain = Chain.<Integer, String>create()
                .useTracing(tracer, Integer.class, String.class)
                .useWhen(request -> true, (request, token) -> completed("handled"))
                .build();

        assertEquals("handled", chain.execute(1).toCompletableFuture().join());

        var span = tracer.spans.get(0);
        assertEquals(ChainDiagnostics.EXECUTE_SPAN_NAME, span.name);
        assertEquals(Integer.class.getName(), span.requestType);
        assertEquals(String.class.getName(), span.responseType);
        assertTrue(span.ok);
        assertTrue(span.closed);
        assertNull(span.error);
    }

    @Test
    void tracingMiddlewareRecordsAsynchronousFailures() {
        var tracer = new RecordingTracer();
        var failure = new IllegalArgumentException("boom");

        var chain = Chain.<Integer, String>create()
                .useTracing(tracer, "custom", null, null)
                .useWhen(request -> true, (request, token) -> CompletableFuture.failedFuture(failure))
                .build();

        assertThrows(CompletionException.class, () -> chain.execute(1).toCompletableFuture().join());

        var span = tracer.spans.get(0);
        assertEquals("custom", span.name);
        assertEquals(failure, span.error);
        assertTrue(span.closed);
    }

    @Test
    void tracingMiddlewareRecordsSynchronousFailures() {
        var tracer = new RecordingTracer();

        var chain = Chain.<Integer, String>create()
                .useTracing(tracer, Integer.class, String.class)
                .use((request, next, token) -> {
                    throw new IllegalStateException("sync boom");
                })
                .build();

        assertThrows(IllegalStateException.class, () -> chain.execute(1));

        var span = tracer.spans.get(0);
        assertInstanceOf(IllegalStateException.class, span.error);
        assertTrue(span.closed);
    }

    @Test
    void tracingRejectsInvalidArguments() {
        var builder = Chain.<Integer, String>create();
        var tracer = new RecordingTracer();

        assertThrows(NullPointerException.class, () -> builder.useTracing(null, "name", null, null));
        assertThrows(NullPointerException.class, () -> builder.useTracing(tracer, null, null, null));
        assertThrows(IllegalArgumentException.class, () -> builder.useTracing(tracer, "", null, null));
        assertThrows(NullPointerException.class, () -> builder.useTracing(tracer, null, String.class));
        assertThrows(NullPointerException.class, () -> builder.useTracing(tracer, Integer.class, null));
    }

    @Test
    void unhandledRequestExceptionExposesItsMessageAndCause() {
        var cause = new IllegalStateException("cause");

        assertNotNull(new UnhandledRequestException().getMessage());
        assertEquals("custom", new UnhandledRequestException("custom").getMessage());
        assertEquals(cause, new UnhandledRequestException("custom", cause).getCause());
    }

    @Test
    void diagnosticConstantsAreStable() {
        assertEquals("MessageFlow", ChainDiagnostics.TRACER_NAME);
        assertEquals("MessageFlow.Execute", ChainDiagnostics.EXECUTE_SPAN_NAME);
        assertEquals("messageflow.request_type", ChainDiagnostics.REQUEST_TYPE_ATTRIBUTE);
        assertEquals("messageflow.response_type", ChainDiagnostics.RESPONSE_TYPE_ATTRIBUTE);
        assertEquals("1.0.0", ChainDiagnostics.TRACER_VERSION);
    }

    private record LogEntry(ChainLogLevel level, String message, Throwable throwable) {
    }

    private static final class RecordingLogger implements ChainLogger {

        private final boolean enabled;
        private final List<LogEntry> entries = new ArrayList<>();

        RecordingLogger(boolean enabled) {
            this.enabled = enabled;
        }

        @Override
        public boolean isEnabled(ChainLogLevel level) {
            return enabled;
        }

        @Override
        public void log(ChainLogLevel level, String message, Throwable throwable) {
            entries.add(new LogEntry(level, message, throwable));
        }
    }

    private static final class RecordingSpan implements ChainSpan {

        private final String name;
        private final String requestType;
        private final String responseType;
        private boolean ok;
        private boolean closed;
        private Throwable error;

        RecordingSpan(String name, String requestType, String responseType) {
            this.name = name;
            this.requestType = requestType;
            this.responseType = responseType;
        }

        @Override
        public void setOk() {
            ok = true;
        }

        @Override
        public void setError(Throwable throwable) {
            error = throwable;
        }

        @Override
        public void close() {
            closed = true;
        }
    }

    private static final class RecordingTracer implements ChainTracer {

        private final List<RecordingSpan> spans = new ArrayList<>();

        @Override
        public ChainSpan startSpan(String spanName, String requestType, String responseType) {
            var span = new RecordingSpan(spanName, requestType, responseType);
            spans.add(span);
            return span;
        }
    }
}
