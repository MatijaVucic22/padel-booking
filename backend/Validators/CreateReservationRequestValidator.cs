using FluentValidation;
using PadelBooking.Api.DTOs;
using PadelBooking.Api.Services;

namespace PadelBooking.Api.Validators
{
    public class CreateReservationRequestValidator : AbstractValidator<CreateReservationRequest>
    {
        public CreateReservationRequestValidator(IBookingTimeService bookingTime)
        {
            RuleFor(request => request.CourtId)
                .GreaterThan(0).WithMessage("CourtId mora biti veći od 0.");

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
                    endTime - request.StartTime == TimeSpan.FromHours(1))
                .WithMessage("Rezervacija mora trajati tačno 60 minuta.");

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
