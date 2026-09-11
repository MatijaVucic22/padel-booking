namespace PadelBooking.Application.Admin.BlockedPeriods;

public enum CreateBlockedPeriodStatus
{
    Success, CourtNotFound, ReservationOverlap, BlockedPeriodOverlap, LockTimeout
}

public sealed record CreatedBlockedPeriod(
    int Id, int CourtId, string CourtName, DateTime StartTime,
    DateTime EndTime, string Reason);

public sealed record CreateBlockedPeriodResult(
    CreateBlockedPeriodStatus Status, CreatedBlockedPeriod? BlockedPeriod = null);

public enum DeleteBlockedPeriodStatus { Success, NotFound, LockTimeout }

public sealed record DeleteBlockedPeriodResult(DeleteBlockedPeriodStatus Status);
