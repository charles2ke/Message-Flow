# MessageFlow for Java

The Java port of the MessageFlow **Chain of Responsibility** library. It mirrors the .NET API: an
ordered set of handlers is composed once, at build time, into an immutable pipeline that a request
travels until a handler accepts it.

The library is dependency-free and targets Java 17.

## Installation

Build and install the artifact locally:

```bash
cd java
mvn install
```

Then depend on it:

```xml
<dependency>
  <groupId>io.github.messageflow</groupId>
  <artifactId>messageflow</artifactId>
  <version>1.0.0</version>
</dependency>
```

## Quick start

```java
import io.github.messageflow.Chain;
import java.util.concurrent.CompletableFuture;

Chain<Integer, String> chain = Chain.<Integer, String>create()
        .useWhen(request -> request < 0, (request, token) -> CompletableFuture.completedFuture("negative:" + request))
        .useWhen(request -> request == 0, (request, token) -> CompletableFuture.completedFuture("zero"))
        .withFallback((request, token) -> CompletableFuture.completedFuture("positive:" + request))
        .build();

String response = chain.execute(-7).toCompletableFuture().join(); // "negative:-7"
```

### Reusable handlers

```java
public final class RefundHandler extends HandlerBase<Ticket, String> {

    @Override
    protected boolean canHandle(Ticket request) {
        return request.kind() == TicketKind.REFUND;
    }

    @Override
    protected CompletionStage<String> process(Ticket request, CancellationToken cancellationToken) {
        return CompletableFuture.completedFuture("refund:" + request.id());
    }
}
```

Add it with `builder.use(new RefundHandler())`.

### Middleware

A handler may run code before *and* after the rest of the chain:

```java
builder.use((request, next, token) -> {
    long started = System.nanoTime();
    return next.handle(request, token)
            .thenApply(response -> response + " (" + (System.nanoTime() - started) + " ns)");
});
```

### Branches, merging and fallbacks

```java
builder.useBranch(request -> request > 10, branch -> branch
        .useWhen(request -> request > 100, (request, token) -> CompletableFuture.completedFuture("huge")));

builder.use(otherBuilder);   // merge a chain fragment
builder.use(otherChain);     // merge an already built chain
builder.withFallback((request, token) -> CompletableFuture.completedFuture("fallback"));
```

When no handler accepts a request and no fallback is configured, the returned stage completes
exceptionally with `UnhandledRequestException`.

### Cancellation

`CancellationToken` is propagated to every handler. Create one with a `CancellationTokenSource`;
`CancellationToken.none()` is used when `execute(request)` is called without a token.

```java
CancellationTokenSource source = new CancellationTokenSource();
CompletionStage<String> response = chain.execute(request, source.token());
source.cancel();
```

### Observability

Both middlewares wrap the remainder of the chain, so register them first to observe the whole chain.

```java
builder.useLogging(logger, ChainLogLevel.INFORMATION, "orders");
builder.useTracing(tracer, Integer.class, String.class);
```

`ChainLogger` and `ChainTracer` are minimal interfaces, so adapters over SLF4J or OpenTelemetry are a
few lines of code. The logging middleware never writes the request itself to the log.

## Public API

| Type | Kind | Description |
| --- | --- | --- |
| `Chain<T, R>` | interface | An immutable, pre-compiled chain of responsibility, plus the `create()` factory. |
| `ComposedChain<T, R>` | class | Default `Chain` implementation, composed once at build time. |
| `ChainBuilder<T, R>` | class | Builds an immutable chain from an ordered set of handlers. |
| `Handler<T, R>` | interface | A single link of a chain of responsibility. |
| `HandlerBase<T, R>` | class | Convenience base class for handlers that either fully handle a request or pass it on. |
| `NextHandler<T, R>` | interface | Represents the next step of a chain of responsibility. |
| `CancellationToken` | class | Propagates a cancellation request through a chain. |
| `CancellationTokenSource` | class | Creates cancellation tokens and signals their cancellation. |
| `ChainLogger` | interface | Receives the log entries a chain writes while executing a request. |
| `ChainLogLevel` | enum | The severity of an entry written by a chain to a `ChainLogger`. |
| `ChainTracer` | interface | Creates the spans emitted by the tracing middleware. |
| `ChainSpan` | interface | A unit of tracing work covering the execution of the remainder of a chain. |
| `ChainDiagnostics` | class | The diagnostic primitives exposed to tracing infrastructure. |
| `UnhandledRequestException` | class | Signals that no handler accepted the request and no fallback was configured. |

## Build and test

```bash
cd java
mvn verify
```

The build treats compiler warnings as errors and runs the JUnit 5 test suite.

## Differences from the .NET library

- Asynchrony uses `CompletionStage` instead of `ValueTask`.
- Because Java erases generics, the logging and tracing middlewares take the chain name and the
  request/response types explicitly instead of deriving them from the type arguments.
- Tracing is exposed through the `ChainTracer` abstraction rather than `System.Diagnostics.Activity`,
  keeping the library dependency-free.
- An unhandled request produces a failed `CompletionStage` instead of a synchronously thrown
  exception.
