using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using PadelBooking.Application.Abstractions.Notifications;
using PadelBooking.Application.Abstractions.Payments;
using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Infrastructure.Persistence;
using Testcontainers.MySql;
using Xunit;

namespace PadelBooking.IntegrationTests;

[CollectionDefinition("mysql-integration", DisableParallelization = true)]
public sealed class MySqlIntegrationCollection : ICollectionFixture<IntegrationTestHost> { }

public sealed class IntegrationTestHost : IAsyncLifetime
{
    private readonly MySqlContainer _mysql = new MySqlBuilder("mysql:8.4")
        .WithDatabase("padel_booking_integration")
        .WithUsername("integration_user")
        .WithPassword(Guid.NewGuid().ToString("N"))
        .Build();

    private TestApiFactory? _factory;

    public TestPaymentGateway Gateway { get; } = new();
    public TestEmailService Email { get; } = new();
    public HttpClient Client { get; private set; } = null!;
    public IServiceProvider Services => _factory!.Services;

    public WebApplicationFactory<Program> CreateIsolatedFactory() =>
        _factory!.WithWebHostBuilder(_ => { });

    public WebApplicationFactory<Program> CreateFactoryWithCourtFailure() =>
        _factory!.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICourtRepository>();
            services.AddScoped<ICourtRepository, ThrowingCourtRepository>();
        }));

    public HttpClient ClientFactoryWithToken(string token)
    {
        var client = _factory!.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task InitializeAsync()
    {
        await _mysql.StartAsync();
        // Minimal-hosting Program reads these values before factory service callbacks.
        // This process gets only disposable test values; no developer secrets are read.
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _mysql.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Key", "integration-tests-only-jwt-key-at-least-32-characters");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "PadelBooking.IntegrationTests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "PadelBooking.IntegrationTests");
        Environment.SetEnvironmentVariable("Cors__AdditionalOrigin", "https://configured-frontend.example.test");
        Environment.SetEnvironmentVariable("STRIPE_SECRET_KEY", string.Empty);
        Environment.SetEnvironmentVariable("STRIPE_WEBHOOK_SECRET", string.Empty);
        Environment.SetEnvironmentVariable("STRIPE_FRONTEND_URL", "https://localhost");
        _factory = new TestApiFactory(_mysql.GetConnectionString(), Gateway, Email);
        Client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });
        // Program runs the real migrations as the test host starts.
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        _factory?.Dispose();
        await _mysql.DisposeAsync();
        foreach (var name in new[]
        {
            "ConnectionStrings__DefaultConnection", "Jwt__Key", "Jwt__Issuer", "Jwt__Audience",
            "Cors__AdditionalOrigin",
            "STRIPE_SECRET_KEY", "STRIPE_WEBHOOK_SECRET", "STRIPE_FRONTEND_URL"
        })
            Environment.SetEnvironmentVariable(name, null);
    }
}

internal sealed class TestApiFactory(
    string connectionString,
    TestPaymentGateway gateway,
    TestEmailService email) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTests");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["Jwt:Key"] = "integration-tests-only-jwt-key-at-least-32-characters",
                ["Jwt:Issuer"] = "PadelBooking.IntegrationTests",
                ["Jwt:Audience"] = "PadelBooking.IntegrationTests",
                ["Cors:AdditionalOrigin"] = "https://configured-frontend.example.test",
                ["STRIPE_SECRET_KEY"] = string.Empty,
                ["STRIPE_WEBHOOK_SECRET"] = string.Empty,
                ["STRIPE_FRONTEND_URL"] = "https://localhost"
            }));

        builder.ConfigureTestServices(services =>
        {
            // Force the disposable container connection even if another config source exists.
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseMySQL(connectionString, mysql =>
                    mysql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

            services.RemoveAll<IPaymentGateway>();
            services.AddSingleton<IPaymentGateway>(gateway);
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(email);
            services.RemoveAll<ICourtChangeNotifier>();
            services.AddSingleton<ICourtChangeNotifier, TestCourtChangeNotifier>();
            services.RemoveAll<IReservationNotificationLogger>();
            services.AddSingleton<IReservationNotificationLogger, TestNotificationLogger>();
            services.RemoveAll<IHostedService>();
        });
    }
}
