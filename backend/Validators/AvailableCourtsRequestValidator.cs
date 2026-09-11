using FluentValidation;
using PadelBooking.Application.Abstractions.Time;
using PadelBooking.Api.DTOs;

namespace PadelBooking.Api.Validators
{
    public class AvailableCourtsRequestValidator : AbstractValidator<AvailableCourtsRequest>
    {
        public AvailableCourtsRequestValidator(IBookingTimeService bookingTime)
        {
            RuleFor(request => request.DurationHours)
                .InclusiveBetween(1, 3)
                .WithMessage("Trajanje mora biti 1, 2 ili 3 sata.");

            RuleFor(request => request.StartTime)
                .Must(value => value.Kind == DateTimeKind.Unspecified)
                .WithMessage("Vreme mora biti lokalno bez timezone offset-a.")
                .Must(value => value > bookingTime.Now)
                .WithMessage("Vreme početka mora biti u budućnosti.")
                .Must(value => value.TimeOfDay.Ticks % TimeSpan.TicksPerHour == 0)
                .WithMessage("Vreme početka mora biti na pun sat.")
                .Must(value => value.Hour >= 8 && value.Hour < 22)
                .WithMessage("Termin mora početi između 08:00 i 21:00.");

            RuleFor(request => request)
                .Must(request =>
                {
                    var endTime = request.StartTime.AddHours(request.DurationHours);
                    return endTime.Date == request.StartTime.Date &&
                        endTime.TimeOfDay <= TimeSpan.FromHours(22);
                })
                .WithName("endTime")
                .WithMessage("Termin mora biti istog dana i završiti najkasnije u 22:00.");
        }
    }
}
