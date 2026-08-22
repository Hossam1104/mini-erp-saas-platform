using Microsoft.Data.SqlClient;

namespace MiniErp.Infrastructure.Persistence.Modules.Inventory;

internal static class InventoryPersistenceExceptionClassifier
{
    private const int DeadlockVictim = 1205;
    private const int LockRequestTimeout = 1222;

    internal static bool IsSqlServerContention(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlException
                && sqlException.Number is DeadlockVictim or LockRequestTimeout)
            {
                return true;
            }
        }

        return false;
    }

    internal static InvalidOperationException Unavailable(Exception exception) =>
        new("Inventory persistence is unavailable.", exception);

    internal static bool IsCorrectionUniqueViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlException && sqlException.Number is 2601 or 2627 && current.Message.Contains("CorrectionOfMovementId", StringComparison.OrdinalIgnoreCase)) return true;
            if (current.Message.Contains("CorrectionOfMovementId", StringComparison.OrdinalIgnoreCase) && current.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
