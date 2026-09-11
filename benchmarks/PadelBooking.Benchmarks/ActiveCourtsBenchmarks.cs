using BenchmarkDotNet.Attributes;
using Dapper;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace PadelBooking.Benchmarks;

[MemoryDiagnoser]
public class ActiveCourtsBenchmarks
{
    private const string ConnectionStringEnvironmentVariable =
        "PADELBOOKING_BENCHMARK_CONNECTION_STRING";

    private string _connectionString = null!;
    private DbContextOptions<BenchmarkDbContext> _dbContextOptions = null!;

    [GlobalSetup]
    public void Setup()
    {
        _connectionString = Environment.GetEnvironmentVariable(
            ConnectionStringEnvironmentVariable)
            ?? throw new InvalidOperationException(
                $"Set {ConnectionStringEnvironmentVariable} before running benchmarks.");

        _dbContextOptions = new DbContextOptionsBuilder<BenchmarkDbContext>()
            .UseMySQL(_connectionString)
            .Options;
    }

    [Benchmark(Baseline = true)]
    public async Task<List<CourtReadModel>> GetActiveCourtsEfTracked()
    {
        await using var context = new BenchmarkDbContext(_dbContextOptions);
        return await context.Courts
            .Where(court => court.IsActive)
            .ToListAsync();
    }

    [Benchmark]
    public async Task<List<CourtReadModel>> GetActiveCourtsEfAsNoTracking()
    {
        await using var context = new BenchmarkDbContext(_dbContextOptions);
        return await context.Courts
            .AsNoTracking()
            .Where(court => court.IsActive)
            .ToListAsync();
    }

    [Benchmark]
    public async Task<IReadOnlyList<CourtReadModel>> GetActiveCourtsDapper()
    {
        await using var connection = new MySqlConnection(_connectionString);
        var courts = await connection.QueryAsync<CourtReadModel>(
            """
            SELECT Id, Name, Location, Description, PricePerHour, ImageUrl, IsActive
            FROM Courts
            WHERE IsActive = TRUE
            """);

        return courts.AsList();
    }
}

internal sealed class BenchmarkDbContext(
    DbContextOptions<BenchmarkDbContext> options) : DbContext(options)
{
    public DbSet<CourtReadModel> Courts => Set<CourtReadModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CourtReadModel>(entity =>
        {
            entity.ToTable("Courts");
            entity.HasKey(court => court.Id);
        });
    }
}

public sealed class CourtReadModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal PricePerHour { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; }
}
