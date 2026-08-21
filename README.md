# Message-Flow

A small, dependency-free **Chain of Responsibility** library for .NET.

`MessageFlow` lets you compose an ordered set of handlers into an immutable chain. A request travels
through the chain until a handler accepts it; unhandled requests either hit a configured fallback or
raise `UnhandledRequestException`. The pipeline is composed once at build time, so executing a
request is just a delegate invocation.

<!-- BEGIN AUTO-GENERATED: coverage -->
![line coverage](https://img.shields.io/badge/line%20coverage-100%25-brightgreen)
![branch coverage](https://img.shields.io/badge/branch%20coverage-100%25-brightgreen)
<!-- END AUTO-GENERATED: coverage -->

## Features

- Async-first API built on `ValueTask`, with full `CancellationToken` propagation.
- Immutable, pre-compiled pipelines — no per-request allocation for chain traversal.
- Three ways to add a link: an `IHandler<,>` implementation, the `HandlerBase<,>` convenience class,
  or an inline lambda.
- Middleware-style handlers can run code before *and* after the rest of the chain.
- Optional fallback for unhandled requests.

## Installation

The library targets `net8.0`. Add a project reference:

```bash
dotnet add reference src/MessageFlow/MessageFlow.csproj
```

## Quick start

```csharp
using MessageFlow;

var chain = Chain.Create<int, string>()
    .UseWhen(request => request < 0, (request, _) => new ValueTask<string>($"negative:{request}"))
    .UseWhen(request => request == 0, (_, _) => new ValueTask<string>("zero"))
    .WithFallback((request, _) => new ValueTask<string>($"positive:{request}"))
    .Build();

string response = await chain.ExecuteAsync(-7); // "negative:-7"
```

### Reusable handlers

```csharp
public sealed class RefundHandler : HandlerBase<Ticket, string>
{
    protected override bool CanHandle(Ticket ticket) => ticket.Kind == TicketKind.Refund;

    protected override ValueTask<string> ProcessAsync(Ticket ticket, CancellationToken cancellationToken)
        => new($"refund issued for {ticket.Id}");
}

var chain = Chain.Create<Ticket, string>()
    .Use(new RefundHandler())
    .Use(new EscalationHandler())
    .Build(); // throws UnhandledRequestException when nothing handles the ticket
```

### Middleware-style handlers

An inline handler receives the next step of the chain, so it can wrap the remainder — useful for
logging, timing or result post-processing:

```csharp
var chain = Chain.Create<string, string>()
    .Use(async (request, next, cancellationToken) =>
    {
        var response = await next(request, cancellationToken);
        return response.ToUpperInvariant();
    })
    .WithFallback((request, _) => new ValueTask<string>(request))
    .Build();
```

## Samples

The [`samples/MessageFlow.Samples`](samples/MessageFlow.Samples/README.md) project contains runnable
examples for quick start routing, `HandlerBase<,>` handlers, middleware, fallbacks, cancellation and
a custom retry handler:

```bash
dotnet run --project samples/MessageFlow.Samples/MessageFlow.Samples.csproj
```

## Public API

<!-- BEGIN AUTO-GENERATED: api -->
| Type | Kind | Description |
| --- | --- | --- |
| `Chain&lt;TRequest, TResponse&gt;` | class | Default IChain&lt;TRequest, TResponse&gt; implementation. The handler pipeline is composed once, at build time, so execution is a simple delegate call. |
| `ChainBuilder&lt;TRequest, TResponse&gt;` | class | Builds an immutable IChain&lt;TRequest, TResponse&gt; from an ordered set of handlers. |
| `Chain` | class | Entry point for creating chains of responsibility. |
| `HandlerBase&lt;TRequest, TResponse&gt;` | class | Convenience base class for handlers that either fully handle a request or pass it on. |
| `IChain&lt;TRequest, TResponse&gt;` | interface | An immutable, pre-compiled chain of responsibility. |
| `IHandler&lt;TRequest, TResponse&gt;` | interface | A single link of a chain of responsibility. |
| `NextHandler&lt;TRequest, TResponse&gt;` | delegate | Represents the next step of a chain of responsibility. |
| `UnhandledRequestException` | class | Thrown when no handler of a chain accepted the request and no fallback was configured. |
<!-- END AUTO-GENERATED: api -->

## Repository layout

| Path | Description |
| --- | --- |
| `src/MessageFlow` | The library. |
| `samples/MessageFlow.Samples` | Runnable examples, see [samples/MessageFlow.Samples](samples/MessageFlow.Samples/README.md). |
| `tests/MessageFlow.Tests` | xUnit tests, gated at 100% line, branch and method coverage. |
| `benchmarks/MessageFlow.Benchmarks` | BenchmarkDotNet performance benchmarks. |
| `scripts/update_readme.py` | Regenerates the auto-generated README sections. |

## Build, test and coverage

```bash
dotnet build MessageFlow.slnx
dotnet test tests/MessageFlow.Tests/MessageFlow.Tests.csproj \
  -p:CollectCoverage=true \
  -p:CoverletOutputFormat="cobertura%2cjson" \
  -p:Threshold=100 \
  -p:ThresholdType="line%2cbranch%2cmethod"
```

The build treats warnings (including .NET analyzer diagnostics) as errors, and the test run fails if
coverage drops below 100%.

## Performance

```bash
dotnet run --project benchmarks/MessageFlow.Benchmarks/MessageFlow.Benchmarks.csproj -c Release -- --filter '*'
```

The benchmark compares the pre-compiled chain against a classic linked-list chain of responsibility
for chains of 1, 5 and 20 handlers, and reports allocations via `MemoryDiagnoser`. Benchmarks also run
in CI and their results are uploaded as a build artifact.

## Security

Security scanning runs automatically on every push and pull request:

- **CodeQL** (`.github/workflows/codeql.yml`) with the `security-extended` query suite.
- **Dependency review** (`.github/workflows/dependency-review.yml`) on pull requests.
- **`dotnet list package --vulnerable --include-transitive`** in CI, which fails the build when a
  vulnerable NuGet package (direct or transitive) is detected.

## Auto-updated documentation

The coverage badges and public API table above are generated from the code and the coverage report.
`.github/workflows/update-readme.yml` regenerates them on every push to `main` and commits the
result; pull requests are checked with:

```bash
python scripts/update_readme.py --check
```

## License

Apache-2.0. See [LICENSE](LICENSE).
