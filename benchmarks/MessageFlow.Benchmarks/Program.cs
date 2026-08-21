using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(MessageFlow.Benchmarks.ChainBenchmarks).Assembly).Run(args);
