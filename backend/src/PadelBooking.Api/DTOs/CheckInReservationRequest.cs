using System.ComponentModel.DataAnnotations;

namespace PadelBooking.Api.DTOs;

public sealed class CheckInReservationRequest
{
    [Required]
    public string VerificationToken { get; init; } = string.Empty;
}
