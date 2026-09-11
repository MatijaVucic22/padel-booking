namespace PadelBooking.Application.Admin.Statistics;

public sealed record AdminStatistics(
    int TotalUsers,
    int ActiveCourts,
    int TotalReservations,
    int UpcomingReservations,
    int OngoingReservations,
    int CompletedReservations,
    int CancelledReservations,
    decimal RealizedRevenue,
    decimal UpcomingRevenue);
