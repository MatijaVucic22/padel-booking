using PadelBooking.Application.Admin.Models;

namespace PadelBooking.Application.Admin.Calendar;

public sealed record AdminCalendarResult(DateOnly Date, AdminCalendarData Data);
