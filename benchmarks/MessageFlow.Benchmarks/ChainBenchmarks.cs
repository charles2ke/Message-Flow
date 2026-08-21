using BenchmarkDotNet.Attributes;
using MessageFlow;

namespace MessageFlow.Benchmarks;

/// <summary>
/// Measures the cost of executing a request through chains of different lengths and compares
/// the pre-compiled pipeline against a naive linked-list chain of responsibility implementation.
/// </summary>
[MemoryDiagnoser]
public class ChainBenchmarks
{
    private IChain<int, int> _chain = null!;
    private LinkedHandler _linked = null!;

    /// <summary>
    /// Gets or sets the number of handlers in the chain.
    /// </summary>
    [Params(1, 5, 20)]
    public int HandlerCount { get; set; }

    /// <summary>
    /// Builds the chains used by the benchmarks.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var builder = Chain.Create<int, int>();
        for (var i = 0; i < HandlerCount; i++)
        {
            var index = i;
            builder.UseWhen(request => request == index, (request, _) => new ValueTask<int>(request));
        }

        _chain = builder.WithFallback((request, _) => new ValueTask<int>(-request)).Build();

        LinkedHandler? head = null;
        for (var i = HandlerCount - 1; i >= 0; i--)
        {
            head = new LinkedHandler(i, head);
        }

        _linked = head ?? new LinkedHandler(-1, null);
    }

    /// <summary>
    /// Executes a request that is handled by the last handler of the pre-compiled chain.
    /// </summary>
    /// <returns>The response.</returns>
    [Benchmark(Baseline = true)]
    public ValueTask<int> PrecompiledChain() => _chain.ExecuteAsync(HandlerCount - 1);

    /// <summary>
    /// Executes the same request through a classic linked-list chain.
    /// </summary>
    /// <returns>The response.</returns>
    [Benchmark]
    public int LinkedListChain() => _linked.Handle(HandlerCount - 1);

    private sealed class LinkedHandler(int accepts, LinkedHandler? next)
    {
        public int Handle(int request)
        {
            if (request == accepts)
            {
                return request;
            }

            return next is null ? -request : next.Handle(request);
        }
    }
}
