using FluentValidation;
using PadelBooking.Application.Abstractions.Time;
using PadelBooking.Api.DTOs;

namespace PadelBooking.Api.Validators
{
    internal sealed class ReservationTimeRequestValidator<TRequest> : AbstractValidator<TRequest>
        where TRequest : ReservationTimeRequest
    {
        public ReservationTimeRequestValidator(IBookingTimeService bookingTime)
        {
            RuleFor(request => request.StartTime)
                .Must(startTime => startTime > bookingTime.Now)
                .WithMessage("Vreme početka mora biti u budućnosti.");

            RuleFor(request => request.StartTime)
                .Must(startTime =>
                    startTime.TimeOfDay.Ticks % TimeSpan.TicksPerHour == 0)
                .WithMessage("Vreme početka mora biti na pun sat.");

            RuleFor(request => request.StartTime)
                .Must(startTime => startTime.Hour >= 8 && startTime.Hour < 22)
                .WithMessage("Termin mora početi između 08:00 i 21:00.");

            RuleFor(request => request.EndTime)
                .GreaterThan(request => request.StartTime)
                .WithMessage("Vreme završetka mora biti posle vremena početka.");

            RuleFor(request => request.EndTime)
                .Must((request, endTime) =>
                {
                    var duration = endTime - request.StartTime;
                    return duration >= TimeSpan.FromHours(1) &&
                        duration <= TimeSpan.FromHours(3) &&
                        duration.Ticks % TimeSpan.TicksPerHour == 0;
                })
                .WithMessage("Rezervacija mora trajati 1, 2 ili 3 sata.");

            RuleFor(request => request.EndTime)
                .Must((request, endTime) =>
                    endTime.Date == request.StartTime.Date)
                .WithMessage("Rezervacija ne sme prelaziti preko ponoći.");

            RuleFor(request => request.EndTime)
                .Must(endTime => endTime.TimeOfDay <= TimeSpan.FromHours(22))
                .WithMessage("Termin mora završiti najkasnije u 22:00.");
        }
    }
}
