namespace PadelBooking.Application.Admin.BlockedPeriods;

public sealed record CreateBlockedPeriodCommand(
    int CourtId, DateTime StartTime, DateTime EndTime, string Reason);
