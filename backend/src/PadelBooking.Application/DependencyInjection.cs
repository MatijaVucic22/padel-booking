using Microsoft.Extensions.DependencyInjection;
using PadelBooking.Application.Admin.BlockedPeriods;
using PadelBooking.Application.Admin.Calendar;
using PadelBooking.Application.Admin.Reservations;
using PadelBooking.Application.Admin.Statistics;
using PadelBooking.Application.Admin.Users;
using PadelBooking.Application.Authentication.GetCurrentUser;
using PadelBooking.Application.Authentication.Login;
using PadelBooking.Application.Authentication.Register;
using PadelBooking.Application.Courts.CreateCourt;
using PadelBooking.Application.Courts.DeactivateCourt;
using PadelBooking.Application.Courts.GetAvailableCourts;
using PadelBooking.Application.Courts.GetCourt;
using PadelBooking.Application.Courts.GetCourts;
using PadelBooking.Application.Courts.UpdateCourt;
using PadelBooking.Application.Reservations.Availability;
using PadelBooking.Application.Reservations.Cancel;
using PadelBooking.Application.Reservations.Create;
using PadelBooking.Application.Reservations.MyReservations;
using PadelBooking.Application.Reservations.Reminders;
using PadelBooking.Application.Reservations.Reschedule;

namespace PadelBooking.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<RegisterUser>();
        services.AddScoped<LoginUser>();
        services.AddScoped<GetCurrentUser>();
        services.AddScoped<GetActiveCourts>();
        services.AddScoped<GetActiveCourt>();
        services.AddScoped<GetAvailableCourts>();
        services.AddScoped<CreateCourt>();
        services.AddScoped<UpdateCourt>();
        services.AddScoped<DeactivateCourt>();
        services.AddScoped<CreateReservation>();
        services.AddScoped<GetMyReservations>();
        services.AddScoped<CancelReservation>();
        services.AddScoped<RescheduleReservation>();
        services.AddScoped<GetReservationAvailability>();
        services.AddScoped<ProcessDueReservationReminders>();
        services.AddScoped<GetAdminUsers>();
        services.AddScoped<GetAdminReservations>();
        services.AddScoped<GetAdminStatistics>();
        services.AddScoped<GetAdminCalendar>();
        services.AddScoped<CreateBlockedPeriod>();
        services.AddScoped<DeleteBlockedPeriod>();
        return services;
    }
}
