# MessageFlow samples

Runnable examples for the `MessageFlow` library. Run them all with:

```bash
dotnet run --project samples/MessageFlow.Samples/MessageFlow.Samples.csproj
```

| Sample | Shows |
| --- | --- |
| [`QuickStartSample`](QuickStartSample.cs) | Inline `UseWhen` predicates plus a `WithFallback` terminal step. |
| [`SupportTicketSample`](SupportTicketSample.cs) | Reusable `HandlerBase<,>` handlers routing support tickets, escalating the rest. |
| [`MergedChainsSample`](MergedChainsSample.cs) | Merging two independently authored chain fragments with `Use(ChainBuilder<,>)`. |
| [`MiddlewareSample`](MiddlewareSample.cs) | Middleware-style handlers that run code before *and* after the rest of the chain. |
| [`UnhandledRequestSample`](UnhandledRequestSample.cs) | `UnhandledRequestException` versus a configured fallback. |
| [`CancellationSample`](CancellationSample.cs) | `CancellationToken` propagation through every handler. |
| [`RetrySample`](RetrySample.cs) | A hand-written `IHandler<,>` implementing a retry policy around the chain. |

Every sample exposes a `BuildChain` method (so the chain can be reused or asserted on) and a
`RunAsync(TextWriter, CancellationToken)` method that returns its results. The samples are covered
by `tests/MessageFlow.Tests/SamplesTests.cs` and are part of the repository's 100% coverage gate.
