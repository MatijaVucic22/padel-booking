namespace PadelBooking.Application.Courts.GetCourts;

public sealed record ActiveCourtListItem(
    int Id,
    string Name,
    string Location,
    string? Description,
    decimal PricePerHour,
    string? ImageUrl,
    bool IsActive,
    DateTime? NextAvailableStart);
