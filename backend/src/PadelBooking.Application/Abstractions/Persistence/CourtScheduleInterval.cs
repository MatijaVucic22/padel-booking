namespace PadelBooking.Application.Abstractions.Persistence;

public sealed record CourtScheduleInterval(
    int CourtId,
    DateTime StartTime,
    DateTime EndTime);
