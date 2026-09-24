namespace PadelBooking.Api.Errors;

public static class ApiErrorCodes
{
    public const string Validation = "VALIDATION_ERROR";
    public const string DuplicateEmail = "DUPLICATE_EMAIL";
    public const string InvalidWebhook = "INVALID_WEBHOOK";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string SlotUnavailable = "SLOT_UNAVAILABLE";
    public const string CourtInactive = "COURT_INACTIVE";
    public const string PaymentRequired = "PAYMENT_REQUIRED";
    public const string PaymentPending = "PAYMENT_PENDING";
    public const string RescheduleNotAllowed = "RESCHEDULE_NOT_ALLOWED";
    public const string CancellationNotAllowed = "CANCELLATION_NOT_ALLOWED";
    public const string CancellationNoRefundAcknowledgementRequired =
        "CANCELLATION_NO_REFUND_ACK_REQUIRED";
    public const string CheckoutTooClose = "CHECKOUT_TOO_CLOSE";
    public const string ActivePaymentHold = "ACTIVE_PAYMENT_HOLD";
    public const string BlockedPeriodInPast = "BLOCKED_PERIOD_IN_PAST";
    public const string Conflict = "CONFLICT";
    public const string CourtLockTimeout = "COURT_LOCK_TIMEOUT";
    public const string RateLimited = "RATE_LIMITED";
    public const string RequestFailed = "REQUEST_FAILED";
    public const string ProviderUnavailable = "PROVIDER_UNAVAILABLE";
    public const string Internal = "INTERNAL_ERROR";
}
