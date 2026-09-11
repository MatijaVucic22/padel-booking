namespace PadelBooking.Application.Courts.UpdateCourt;

public sealed record UpdateCourtCommand(
    int Id,
    string Name,
    string Location,
    string? Description,
    decimal PricePerHour);
