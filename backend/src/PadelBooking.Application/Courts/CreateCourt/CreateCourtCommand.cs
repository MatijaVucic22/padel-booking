using PadelBooking.Application.Abstractions.Storage;

namespace PadelBooking.Application.Courts.CreateCourt;

public sealed record CreateCourtCommand(
    string Name,
    string Location,
    string? Description,
    decimal PricePerHour,
    CourtImageUpload? Image);
