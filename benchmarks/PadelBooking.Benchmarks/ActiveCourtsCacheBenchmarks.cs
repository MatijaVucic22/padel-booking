using BenchmarkDotNet.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace PadelBooking.Benchmarks;

[MemoryDiagnoser]
public class ActiveCourtsCacheBenchmarks
{
    private const string ConnectionStringEnvironmentVariable =
        "PADELBOOKING_BENCHMARK_CONNECTION_STRING";
    private const string ColdCacheKey = "active-courts-cold";
    private const string WarmCacheKey = "active-courts-warm";

    private DbContextOptions<BenchmarkDbContext> _dbContextOptions = null!;
    private ServiceProvider _serviceProvider = null!;
    private HybridCache _cache = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            ConnectionStringEnvironmentVariable)
            ?? throw new InvalidOperationException(
                $"Set {ConnectionStringEnvironmentVariable} before running benchmarks.");

        _dbContextOptions = new DbContextOptionsBuilder<BenchmarkDbContext>()
            .UseMySQL(connectionString)
            .Options;

        var services = new ServiceCollection();
        services.AddHybridCache();
        _serviceProvider = services.BuildServiceProvider();
        _cache = _serviceProvider.GetRequiredService<HybridCache>();

        await _cache.GetOrCreateAsync(
            WarmCacheKey,
            cancellationToken => QueryActiveCourtsAsync(cancellationToken));
    }

    [GlobalCleanup]
    public void Cleanup() => _serviceProvider.Dispose();

    [Benchmark(Baseline = true)]
    public Task<List<CourtReadModel>> GetActiveCourtsEfAsNoTracking() =>
        QueryActiveCourtsAsync(CancellationToken.None).AsTask();

    [IterationSetup(Target = nameof(GetActiveCourtsHybridCacheCold))]
    public void RemoveActiveCourtsCacheEntry() =>
        _cache.RemoveAsync(ColdCacheKey)
            .AsTask()
            .GetAwaiter()
            .GetResult();

    [Benchmark]
    [InvocationCount(1)]
    public ValueTask<List<CourtReadModel>> GetActiveCourtsHybridCacheCold() =>
        _cache.GetOrCreateAsync(
            ColdCacheKey,
            cancellationToken => QueryActiveCourtsAsync(cancellationToken));

    [Benchmark]
    public ValueTask<List<CourtReadModel>> GetActiveCourtsHybridCacheWarm() =>
        _cache.GetOrCreateAsync(
            WarmCacheKey,
            cancellationToken => QueryActiveCourtsAsync(cancellationToken));

    private async ValueTask<List<CourtReadModel>> QueryActiveCourtsAsync(
        CancellationToken cancellationToken)
    {
        await using var context = new BenchmarkDbContext(_dbContextOptions);
        return await context.Courts
            .AsNoTracking()
            .Where(court => court.IsActive)
            .ToListAsync(cancellationToken);
    }
}
