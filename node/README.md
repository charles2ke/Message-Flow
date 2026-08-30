# MessageFlow for Node.js

The Node.js/TypeScript port of the MessageFlow **Chain of Responsibility** library. It mirrors the
Java and .NET APIs: an ordered set of handlers is composed once, at build time, into an immutable
pipeline that a request travels until a handler accepts it.

The library is dependency-free, runs in both Node.js and browsers, and targets ES2022.

## Installation

Add the package from npm:

```bash
npm install @charles2ke/messageflow
```

To build from source instead, clone the repository and run:

```bash
cd node
npm install
npm run build
```

## Quick start

```typescript
import { createChain } from '@charles2ke/messageflow';

const chain = createChain<number, string>()
  .useWhen(
    (request) => request < 0,
    (request, _token) => Promise.resolve(`negative:${request}`)
  )
  .useWhen(
    (request) => request === 0,
    (_request, _token) => Promise.resolve('zero')
  )
  .withFallback((request, _token) => Promise.resolve(`positive:${request}`))
  .build();

const response = await chain.execute(-7); // "negative:-7"
```

### Reusable handlers

```typescript
import { HandlerBase, CancellationToken } from '@charles2ke/messageflow';

class RefundHandler extends HandlerBase<Ticket, string> {
  protected canHandle(request: Ticket): boolean {
    return request.kind === TicketKind.Refund;
  }

  protected process(
    request: Ticket,
    cancellationToken: CancellationToken
  ): Promise<string> {
    return Promise.resolve(`refund:${request.id}`);
  }
}
```

Add it with `builder.use(new RefundHandler())`.

### Middleware

A handler may run code before *and* after the rest of the chain:

```typescript
builder.use(async (request, next, token) => {
  const started = performance.now();
  const response = await next(request, token);
  return `${response} (${(performance.now() - started).toFixed(3)} ms)`;
});
```

### Branches, merging and fallbacks

```typescript
builder.useBranch(
  (request) => request > 10,
  (branch) =>
    branch.useWhen(
      (request) => request > 100,
      (request, _token) => Promise.resolve('huge')
    )
);

builder.use(otherBuilder); // merge a chain fragment
builder.use(otherChain); // merge an already built chain
builder.withFallback((request, _token) => Promise.resolve('fallback'));
```

When no handler accepts a request and no fallback is configured, the returned promise rejects with
`UnhandledRequestError`.

### Cancellation

`CancellationToken` is propagated to every handler. Create one with a `CancellationTokenSource`;
`CancellationToken.none()` is used when `execute(request)` is called without a token.

```typescript
import { CancellationTokenSource } from '@charles2ke/messageflow';

const source = new CancellationTokenSource();
const responsePromise = chain.execute(request, source.token());
source.cancel();
```

### Observability

Both middlewares wrap the remainder of the chain, so register them first to observe the whole chain.

```typescript
builder.useLogging(logger, ChainLogLevel.Information, 'orders');
builder.useTracing(tracer, 'MessageFlow.Execute', 'number', 'string');
```

`ChainLogger` and `ChainTracer` are minimal interfaces, so adapters over any logging or tracing
framework are a few lines of code. The logging middleware never writes the request itself to the
log.

## Public API

| Type | Kind | Description |
| --- | --- | --- |
| `Chain<T, R>` | interface | An immutable, pre-compiled chain of responsibility. |
| `createChain<T, R>()` | function | Factory function that creates a new `ChainBuilder`. |
| `ComposedChain<T, R>` | class | Default `Chain` implementation, composed once at build time. |
| `ChainBuilder<T, R>` | class | Builds an immutable chain from an ordered set of handlers. |
| `Handler<T, R>` | interface | A single link of a chain of responsibility. |
| `HandlerBase<T, R>` | class | Convenience base class for handlers that either fully handle a request or pass it on. |
| `NextHandler<T, R>` | type | Represents the next step of a chain of responsibility. |
| `CancellationToken` | class | Propagates a cancellation request through a chain. |
| `CancellationTokenSource` | class | Creates cancellation tokens and signals their cancellation. |
| `ChainLogger` | interface | Receives the log entries a chain writes while executing a request. |
| `ChainLogLevel` | enum | The severity of an entry written by a chain to a `ChainLogger`. |
| `ChainTracer` | interface | Creates the spans emitted by the tracing middleware. |
| `ChainSpan` | interface | A unit of tracing work covering the execution of the remainder of a chain. |
| `ChainDiagnostics` | class | The diagnostic primitives exposed to tracing infrastructure. |
| `UnhandledRequestError` | class | Thrown when no handler accepted the request and no fallback was configured. |

## Build and test

```bash
cd node
npm install
npm run build
npm test
```

The build treats compiler warnings and unused variables as errors. Tests use the built-in Node.js
test runner.

## Differences from the .NET and Java libraries

- Asynchrony uses native JavaScript `Promise` instead of `ValueTask` (.NET) or `CompletionStage`
  (Java).
- The factory function is `createChain()` instead of a static `Chain.create()` method, to better
  match JavaScript conventions.
- Error handling uses `UnhandledRequestError` (an `Error` subclass) instead of
  `UnhandledRequestException`.
- Cancellation errors are plain `Error` objects with `name = 'CancellationError'` instead of a
  dedicated exception class.
- The compiled output is plain ESM that runs unmodified in browsers as well as Node.js, with no
  Node.js built-in modules in the library code.
- Tracing is exposed through the `ChainTracer` abstraction, keeping the library dependency-free and
  browser-compatible.
