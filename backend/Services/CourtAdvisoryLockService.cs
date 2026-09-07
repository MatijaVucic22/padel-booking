using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PadelBooking.Api.Data;
using System.Data;

namespace PadelBooking.Api.Services
{
    public interface ICourtAdvisoryLockService
    {
        Task<IAsyncDisposable?> TryAcquireAsync(
            int courtId,
            CancellationToken cancellationToken = default
        );
    }

    public sealed class CourtAdvisoryLockService : ICourtAdvisoryLockService
    {
        private const int LockTimeoutSeconds = 10;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CourtAdvisoryLockService> _logger;

        public CourtAdvisoryLockService(
            ApplicationDbContext context,
            ILogger<CourtAdvisoryLockService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IAsyncDisposable?> TryAcquireAsync(
            int courtId,
            CancellationToken cancellationToken = default)
        {
            var database = _context.Database;
            var lockName = GetLockName(courtId);

            await database.OpenConnectionAsync(cancellationToken);

            try
            {
                await using var command = database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT GET_LOCK(@lockName, @timeoutSeconds);";

                var lockNameParameter = command.CreateParameter();
                lockNameParameter.ParameterName = "@lockName";
                lockNameParameter.Value = lockName;
                command.Parameters.Add(lockNameParameter);

                var timeoutParameter = command.CreateParameter();
                timeoutParameter.ParameterName = "@timeoutSeconds";
                timeoutParameter.DbType = DbType.Int32;
                timeoutParameter.Value = LockTimeoutSeconds;
                command.Parameters.Add(timeoutParameter);

                var result = await command.ExecuteScalarAsync(cancellationToken);

                if (Convert.ToInt32(result) != 1)
                {
                    await CloseConnectionSafelyAsync(database);
                    return null;
                }

                return new CourtAdvisoryLockLease(
                    database,
                    lockName,
                    _logger
                );
            }
            catch
            {
                await CloseConnectionSafelyAsync(database);
                throw;
            }
        }

        private static string GetLockName(int courtId) =>
            $"padelbooking:reservation:court:{courtId}";

        private async Task CloseConnectionSafelyAsync(DatabaseFacade database)
        {
            try
            {
                await database.CloseConnectionAsync();
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "MySQL connection could not be closed during advisory-lock cleanup."
                );
            }
        }

        private sealed class CourtAdvisoryLockLease : IAsyncDisposable
        {
            private readonly DatabaseFacade _database;
            private readonly string _lockName;
            private readonly ILogger _logger;
            private bool _disposed;

            public CourtAdvisoryLockLease(
                DatabaseFacade database,
                string lockName,
                ILogger logger)
            {
                _database = database;
                _lockName = lockName;
                _logger = logger;
            }

            public async ValueTask DisposeAsync()
            {
                if (_disposed) return;
                _disposed = true;

                try
                {
                    await using var command = _database
                        .GetDbConnection()
                        .CreateCommand();

                    command.CommandText = "SELECT RELEASE_LOCK(@lockName);";

                    var lockNameParameter = command.CreateParameter();
                    lockNameParameter.ParameterName = "@lockName";
                    lockNameParameter.Value = _lockName;
                    command.Parameters.Add(lockNameParameter);

                    await command.ExecuteScalarAsync(CancellationToken.None);
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        exception,
                        "MySQL advisory lock could not be explicitly released."
                    );
                }
                finally
                {
                    try
                    {
                        await _database.CloseConnectionAsync();
                    }
                    catch (Exception exception)
                    {
                        _logger.LogWarning(
                            exception,
                            "MySQL connection could not be closed after advisory-lock use."
                        );
                    }
                }
            }
        }
    }
}
