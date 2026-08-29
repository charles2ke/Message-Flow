# MessageFlow for Python

The Python port of the MessageFlow **Chain of Responsibility** library. It mirrors the .NET and Java APIs: an
ordered set of handlers is composed once, at build time, into an immutable pipeline that a request
travels until a handler accepts it.

The library is dependency-free and targets Python 3.9+.

## Installation

Install the package from PyPI:

```bash
pip install messageflow
```

To build from source instead, clone the repository and install locally:

```bash
cd python
pip install .
```

## Quick start

```python
import asyncio
from messageflow import Chain

async def negative_handler(request, token):
    return f"negative:{request}"

async def zero_handler(request, token):
    return "zero"

async def positive_handler(request, token):
    return f"positive:{request}"

chain = (
    Chain.create()
    .use_when(lambda request: request < 0, negative_handler)
    .use_when(lambda request: request == 0, zero_handler)
    .with_fallback(positive_handler)
    .build()
)

response = asyncio.run(chain.execute(-7))  # "negative:-7"
```

### Reusable handlers

```python
from messageflow import HandlerBase, CancellationToken

class RefundHandler(HandlerBase[Ticket, str]):
    def can_handle(self, request: Ticket) -> bool:
        return request.kind == TicketKind.REFUND

    async def process(self, request: Ticket, cancellation_token: CancellationToken) -> str:
        return f"refund:{request.id}"
```

Add it with `builder.use(RefundHandler())`.

### Middleware

A handler may run code before *and* after the rest of the chain:

```python
import time

async def timing_middleware(request, next_handler, token):
    started = time.perf_counter()
    response = await next_handler(request, token)
    elapsed = (time.perf_counter() - started) * 1000
    return f"{response} ({elapsed:.3f} ms)"

builder = Chain.create()
builder.use(timing_middleware)
```

### Branches, merging and fallbacks

```python
async def huge_handler(request, token):
    return "huge"

async def fallback_handler(request, token):
    return "fallback"

builder = Chain.create()
builder.use_branch(
    lambda request: request > 10,
    lambda branch: branch.use_when(
        lambda request: request > 100,
        huge_handler
    )
)

builder.use(other_builder)   # merge a chain fragment
builder.use(other_chain)     # merge an already built chain
builder.with_fallback(fallback_handler)
```

When no handler accepts a request and no fallback is configured, `UnhandledRequestError` is raised.

### Cancellation

`CancellationToken` is propagated to every handler. Create one with a `CancellationTokenSource`;
`CancellationToken.none()` is used when `execute(request)` is called without a token.

```python
from messageflow import CancellationTokenSource

source = CancellationTokenSource()
response = await chain.execute(request, source.token())
source.cancel()
```

### Observability

Both middlewares wrap the remainder of the chain, so register them first to observe the whole chain.

```python
builder.use_logging(logger, ChainLogLevel.INFORMATION, "orders")
builder.use_tracing(tracer, "MessageFlow.Execute", "int", "str")
```

`ChainLogger` and `ChainTracer` are minimal interfaces, so adapters over the standard `logging` module
or OpenTelemetry are a few lines of code. The logging middleware never writes the request itself to the log.

## Public API

| Type | Kind | Description |
| --- | --- | --- |
| `Chain[T, R]` | abstract class | An immutable, pre-compiled chain of responsibility, plus the `create()` factory. |
| `ComposedChain[T, R]` | class | Default `Chain` implementation, composed once at build time. |
| `ChainBuilder[T, R]` | class | Builds an immutable chain from an ordered set of handlers. |
| `Handler[T, R]` | abstract class | A single link of a chain of responsibility. |
| `HandlerBase[T, R]` | class | Convenience base class for handlers that either fully handle a request or pass it on. |
| `NextHandler[T, R]` | type alias | Represents the next step of a chain of responsibility. |
| `CancellationToken` | class | Propagates a cancellation request through a chain. |
| `CancellationTokenSource` | class | Creates cancellation tokens and signals their cancellation. |
| `ChainLogger` | abstract class | Receives the log entries a chain writes while executing a request. |
| `ChainLogLevel` | enum | The severity of an entry written by a chain to a `ChainLogger`. |
| `ChainTracer` | abstract class | Creates the spans emitted by the tracing middleware. |
| `ChainSpan` | abstract class | A unit of tracing work covering the execution of the remainder of a chain. |
| `ChainDiagnostics` | class | The diagnostic primitives exposed to tracing infrastructure. |
| `UnhandledRequestError` | exception | Raised when no handler accepted the request and no fallback was configured. |

## Build and test

```bash
cd python
pip install -e ".[dev]"
pytest --cov=messageflow --cov-report=term-missing --cov-fail-under=100
```

The test suite uses pytest and aims for 100% code coverage.

## Differences from the .NET library

- Asynchrony uses `async`/`await` and awaitables instead of `ValueTask`.
- Type hints use `Generic[T, R]` instead of C# generics.
- Exceptions use `UnhandledRequestError` (subclass of `RuntimeError`) instead of `UnhandledRequestException`.
- Cancellation uses `asyncio.CancelledError` instead of `OperationCanceledException`.
- The tracing middleware is exposed through the `ChainTracer` abstraction rather than `System.Diagnostics.Activity`,
  keeping the library dependency-free.
- Method names use `snake_case` (e.g., `use_when`, `with_fallback`) instead of PascalCase.
