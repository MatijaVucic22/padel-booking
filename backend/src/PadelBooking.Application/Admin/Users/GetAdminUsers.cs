using PadelBooking.Application.Abstractions.Persistence;
using PadelBooking.Application.Admin.Models;

namespace PadelBooking.Application.Admin.Users;

public sealed class GetAdminUsers
{
    private readonly IAdminReadRepository _repository;
    public GetAdminUsers(IAdminReadRepository repository) => _repository = repository;
    public Task<IReadOnlyList<AdminUserItem>> ExecuteAsync(
        CancellationToken cancellationToken = default) =>
        _repository.ListUsersAsync(cancellationToken);
}
