# PadelBooking read benchmarks

This project compares three ways of reading all active rows from the existing
MySQL `Courts` table:

- `GetActiveCourtsEfTracked`: a normal EF Core query with change tracking.
- `GetActiveCourtsEfAsNoTracking`: the same EF Core query without tracking.
- `GetActiveCourtsDapper`: a parameter-free Dapper query mapped to the same
  minimal court shape.

Each benchmark opens a real database connection and materializes the complete
result set. Run against a development database with representative data, not a
production database.

The first experiment compares ORM/data-access overhead while every invocation
queries MySQL. The separate `ActiveCourtsCacheBenchmarks` experiment compares
three paths that return equivalent active-court data:

- Direct EF: request -> MySQL -> result.
- HybridCache cold: request -> cache MISS -> MySQL -> populate cache -> result.
- HybridCache warm: request -> cache HIT -> result.

The cache factory executes the same EF Core `AsNoTracking` query as the direct
database benchmark. The warm entry is populated during global setup. For the
cold benchmark, a targeted iteration setup removes that same entry and the
benchmark is configured for one invocation per iteration. Therefore every
measured invocation starts cold while removal remains outside the measured time.

A warm HybridCache hit normally avoids the database round-trip. Caching can be
appropriate for relatively stable court metadata, but reservation availability
must remain database-backed to preserve correctness.

A cold cache read is expected to be similar to or somewhat slower than a direct
database read because it still queries MySQL and additionally populates cache.

## Configuration

Set the connection string only for the current shell. Do not commit credentials:

```powershell
$env:PADELBOOKING_BENCHMARK_CONNECTION_STRING = "Server=localhost;Port=3306;Database=padel_booking;User=...;Password=..."
```

## Run

From this directory:

```powershell
dotnet run --configuration Release
```

Run only the cache experiment with:

```powershell
dotnet run --configuration Release -- --filter *ActiveCourtsCacheBenchmarks*
```

BenchmarkDotNet writes its normal console summary and detailed artifacts under
`BenchmarkDotNet.Artifacts`.

## Measured results

First benchmark run; all three approaches queried the same active `Courts` data
from the same MySQL database.

| Method | Mean | Ratio | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: |
| GetActiveCourtsEfTracked | 3.051 ms | 1.00 | 111.29 KB | 1.00 |
| GetActiveCourtsEfAsNoTracking | 3.014 ms | 0.99 | 109.04 KB | 0.98 |
| GetActiveCourtsDapper | 1.794 ms | 0.59 | 10.32 KB | 0.09 |

In this run, Dapper had about 41% lower mean latency and allocated about 91%
less managed memory than tracked EF Core. `AsNoTracking` was only slightly
faster than tracked EF Core for this specific small query. These results apply
only to this workload and environment and do not mean that Dapper is always
better than EF Core.

### HybridCache experiment

| Scenario | Mean | Median | Allocated |
| --- | ---: | ---: | ---: |
| EF Core AsNoTracking / MySQL | 2968.423 us | 2955.112 us | 109.04 KB |
| HybridCache cold miss | 5641.670 us | 5277.800 us | 147.97 KB |
| HybridCache warm hit | 5.395 us | 5.381 us | 2.20 KB |

In this run, the warm cache was approximately 550x faster than the direct EF
Core database read, with approximately 99.82% lower latency and 98% lower
managed allocations. The cold cache was approximately 1.9x slower and allocated
approximately 36% more managed memory than direct EF because it performs a
cache lookup, a real database query, and cache population. Its results also
showed higher variance because every measured invocation includes that database
round-trip and cache population.

BenchmarkDotNet does not display a Ratio for the cold benchmark because it uses
a separate job with `InvocationCount=1` and `UnrollFactor=1` to guarantee a real
cache miss for every measured invocation. These results apply only to this
workload and environment and should not be generalized to every workload or
machine.

## Conclusion

EF Core remains suitable for the main application because of its
maintainability, change tracking, relationship support, and migrations. Dapper
can be considered for simple, performance-sensitive read paths, while
HybridCache is useful for relatively stable data such as court metadata.
Reservation availability and booking validation should remain database-backed
because consistency and concurrency correctness are more important for those
operations.
