using PadelBooking.Application.Abstractions.Payments;
using PadelBooking.Domain.Entities;

namespace PadelBooking.Api.Services;

public sealed class PaymentResolutionLogger(ILogger<PaymentResolutionLogger> logger) : IPaymentResolutionLogger
{
    public void LogRequiresResolution(int paymentId, int reservationId, PaymentPurpose purpose, string reasonCode) =>
        logger.LogWarning(
            "Plaćanje zahteva razrešenje. PaymentId={PaymentId}, ReservationId={ReservationId}, PaymentPurpose={PaymentPurpose}, ReasonCode={ReasonCode}.",
            paymentId, reservationId, purpose, reasonCode);
}
