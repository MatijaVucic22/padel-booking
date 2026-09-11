namespace PadelBooking.Application.Admin.Models;

public sealed record AdminUserItem(
    int Id, string FirstName, string LastName, string Email,
    string Role, DateTime CreatedAt);

public sealed record AdminReservationItem(
    int Id, int UserId, string UserName, string UserEmail,
    int CourtId, string CourtName, DateTime StartTime, DateTime EndTime,
    decimal TotalPrice, string Status, DateTime CreatedAt);

public sealed record AdminReservationStatistic(
    DateTime StartTime, DateTime EndTime, decimal TotalPrice, string Status);

public sealed record AdminCalendarCourt(int Id, string Name);

public sealed record AdminCalendarReservation(
    int Id, int CourtId, string CourtName, string UserName, string UserEmail,
    DateTime StartTime, DateTime EndTime, decimal TotalPrice, string Status);

public sealed record AdminCalendarBlockedPeriod(
    int Id, int CourtId, string CourtName, DateTime StartTime,
    DateTime EndTime, string Reason);

public sealed record AdminCalendarData(
    IReadOnlyList<AdminCalendarCourt> Courts,
    IReadOnlyList<AdminCalendarReservation> Reservations,
    IReadOnlyList<AdminCalendarBlockedPeriod> BlockedPeriods);
