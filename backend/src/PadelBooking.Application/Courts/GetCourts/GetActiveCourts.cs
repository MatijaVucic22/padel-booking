using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Domain.Entities;

namespace PadelBooking.Application.Courts.GetCourts;

public sealed class GetActiveCourts
{
    private readonly ICourtRepository _courts;

    public GetActiveCourts(ICourtRepository courts) => _courts = courts;

    public Task<IReadOnlyList<Court>> ExecuteAsync(
        string? location,
        CancellationToken cancellationToken = default) =>
        _courts.ListActiveAsync(
            string.IsNullOrWhiteSpace(location) ? null : location.Trim(),
            cancellationToken);
}
