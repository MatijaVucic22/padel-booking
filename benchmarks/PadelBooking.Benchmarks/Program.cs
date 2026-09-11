using BenchmarkDotNet.Running;
using PadelBooking.Benchmarks;

BenchmarkSwitcher
    .FromAssembly(typeof(ActiveCourtsBenchmarks).Assembly)
    .Run(args);
