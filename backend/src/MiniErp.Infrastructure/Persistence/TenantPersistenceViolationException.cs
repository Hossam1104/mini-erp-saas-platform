namespace MiniErp.Infrastructure.Persistence;

/// <summary>
/// Safe failure raised when a tenant persistence invariant is violated.
/// </summary>
public sealed class TenantPersistenceViolationException : InvalidOperationException
{
    /// <summary>Creates a safe tenant-boundary failure.</summary>
    public TenantPersistenceViolationException(string message)
        : base(message)
    {
    }
}
