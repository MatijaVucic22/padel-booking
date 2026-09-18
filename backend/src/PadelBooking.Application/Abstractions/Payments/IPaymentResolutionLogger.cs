using PadelBooking.Domain.Entities;

namespace PadelBooking.Application.Abstractions.Payments;

public interface IPaymentResolutionLogger
{
    void LogRequiresResolution(int paymentId, int reservationId, PaymentPurpose purpose, string reasonCode);
}
