using MiniErp.App.BuildingBlocks.Tenancy;

namespace MiniErp.Infrastructure.Persistence;

/// <summary>
/// Purpose categories for future reviewed maintenance operations.
/// </summary>
public enum PrivilegedPersistencePurpose
{
    /// <summary>Purpose-bound platform maintenance.</summary>
    Maintenance = 1,
    /// <summary>Purpose-bound bulk repair.</summary>
    BulkRepair = 2,
    /// <summary>Purpose-bound migration operation.</summary>
    Migration = 3
}

/// <summary>
/// Explicit audit input required by a future privileged persistence seam.
/// </summary>
public sealed class PrivilegedPersistenceRequest
{
    /// <summary>Creates explicit governance, purpose and correlation evidence.</summary>
    public PrivilegedPersistenceRequest(
        PlatformGovernanceContext governanceContext,
        PrivilegedPersistencePurpose purpose,
        CorrelationId correlationId)
    {
        GovernanceContext = governanceContext ?? throw new ArgumentNullException(nameof(governanceContext));
        if (!Enum.IsDefined(purpose))
        {
            throw new ArgumentOutOfRangeException(nameof(purpose));
        }

        if (string.IsNullOrWhiteSpace(correlationId.Value)
            || correlationId != governanceContext.CorrelationId)
        {
            throw new ArgumentException(
                "Privileged persistence evidence must use the governance correlation identifier.",
                nameof(correlationId));
        }

        Purpose = purpose;
        CorrelationId = correlationId;
    }

    /// <summary>The separate platform governance context.</summary>
    public PlatformGovernanceContext GovernanceContext { get; }

    /// <summary>The approved privileged operation purpose.</summary>
    public PrivilegedPersistencePurpose Purpose { get; }

    /// <summary>The audit correlation identifier.</summary>
    public CorrelationId CorrelationId { get; }
}

/// <summary>
/// Internal-only seam for future purpose-bound, reviewed unscoped operations.
/// No implementation or workflow is provided by MESP-58.
/// </summary>
internal interface IPrivilegedPersistenceBoundary
{
    Task ExecuteAsync(
        PrivilegedPersistenceRequest request,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken);
}
