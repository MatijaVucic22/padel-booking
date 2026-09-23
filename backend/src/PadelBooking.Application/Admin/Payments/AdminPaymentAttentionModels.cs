namespace PadelBooking.Application.Admin.Payments;

public sealed record AdminPaymentAttentionItem(
    int PaymentId,
    int ReservationId,
    int UserId,
    string UserName,
    string UserEmail,
    int CourtId,
    string CourtName,
    string PaymentPurpose,
    string PaymentStatus,
    string FulfillmentStatus,
    decimal Amount,
    string Currency,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    string? ResolutionReasonCode,
    DateTime? TargetStartTime,
    DateTime? TargetEndTime);

public sealed record AdminPaymentAttentionPage(
    IReadOnlyList<AdminPaymentAttentionItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
