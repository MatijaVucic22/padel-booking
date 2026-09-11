using PadelBooking.Application.Abstractions.Persistence;

namespace PadelBooking.Application.Admin.Calendar;

public sealed class GetAdminCalendar
{
    private readonly IAdminReadRepository _repository;
    public GetAdminCalendar(IAdminReadRepository repository) => _repository = repository;

    public async Task<AdminCalendarResult> ExecuteAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var dayStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var data = await _repository.GetCalendarAsync(
            dayStart, dayStart.AddDays(1), cancellationToken);
        return new(date, data);
    }
}
