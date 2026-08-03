namespace MiniErp.App.BuildingBlocks.Tenancy;

/// <summary>
/// Purpose-bound platform control-plane operation categories.
/// </summary>
public enum PlatformGovernancePurpose
{
    /// <summary>Platform-owned metadata operations.</summary>
    PlatformMetadata = 1,
    /// <summary>Security and audit evidence operations.</summary>
    SecurityEvidence = 2,
    /// <summary>Tenant lifecycle metadata operations.</summary>
    TenantLifecycleMetadata = 3,
    /// <summary>Support governance records, not Tenant business data.</summary>
    SupportGovernance = 4
}

/// <summary>
/// Immutable control-plane context, deliberately separate from TenantContext.
/// </summary>
public sealed class PlatformGovernanceContext
{
    /// <summary>Creates a purpose-bound platform control-plane context.</summary>
    public PlatformGovernanceContext(
        Guid actorId,
        PlatformGovernancePurpose purpose,
        CorrelationId correlationId)
    {
        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("Platform actor must not be empty.", nameof(actorId));
        }

        if (!Enum.IsDefined(purpose))
        {
            throw new ArgumentOutOfRangeException(nameof(purpose));
        }

        if (string.IsNullOrWhiteSpace(correlationId.Value))
        {
            throw new ArgumentException("Platform governance requires a correlation identifier.", nameof(correlationId));
        }

        ActorId = actorId;
        Purpose = purpose;
        CorrelationId = correlationId;
    }

    /// <summary>The platform governance actor.</summary>
    public Guid ActorId { get; }

    /// <summary>The approved platform purpose.</summary>
    public PlatformGovernancePurpose Purpose { get; }

    /// <summary>The evidence correlation identifier.</summary>
    public CorrelationId CorrelationId { get; }
}
