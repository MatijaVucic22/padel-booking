using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PadelBooking.Application.Abstractions.Authentication;
using PadelBooking.Application.Abstractions.Concurrency;
using PadelBooking.Application.Abstractions.Notifications;
using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Application.Abstractions.Storage;
using PadelBooking.Application.Abstractions.Time;
using PadelBooking.Infrastructure.Authentication;
using PadelBooking.Infrastructure.Concurrency;
using PadelBooking.Infrastructure.Email;
using PadelBooking.Infrastructure.Persistence;
using PadelBooking.Infrastructure.Persistence.Repositories;
using PadelBooking.Infrastructure.Storage;
using PadelBooking.Infrastructure.Time;

namespace PadelBooking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string courtImageStorageRoot)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseMySQL(
                configuration.GetConnectionString("DefaultConnection")!,
                mySqlOptions => mySqlOptions.MigrationsAssembly(
                    typeof(ApplicationDbContext).Assembly.FullName)));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IBookingTimeService, BookingTimeService>();
        services.AddScoped<ICourtAdvisoryLockService, CourtAdvisoryLockService>();
        services.Configure<EmailOptions>(
            configuration.GetSection(EmailOptions.SectionName));
        services.Configure<JwtOptions>(
            configuration.GetSection(JwtOptions.SectionName));
        services.Configure<CourtImageStorageOptions>(options =>
            options.RootPath = courtImageStorageRoot);
        services.AddScoped<IEmailService, EmailService>();
        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
        services.AddSingleton<IAccessTokenGenerator, JwtAccessTokenGenerator>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICourtRepository, CourtRepository>();
        services.AddScoped<IReservationRepository, ReservationRepository>();
        services.AddScoped<IBlockedPeriodRepository, BlockedPeriodRepository>();
        services.AddScoped<IAdminReadRepository, AdminReadRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICourtImageStorage, LocalCourtImageStorage>();
        return services;
    }
}
