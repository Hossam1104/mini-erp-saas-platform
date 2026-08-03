namespace MiniErp.App.BuildingBlocks.Tenancy;

/// <summary>
/// Strongly typed identity of a Tenant boundary.
/// </summary>
public readonly record struct TenantId
{
    /// <summary>Creates a validated Tenant identifier.</summary>
    public TenantId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("TenantId must not be empty.", nameof(value));
        }

        Value = value;
    }

    /// <summary>The underlying identifier value.</summary>
    public Guid Value { get; }

    /// <summary>Returns the canonical identifier representation.</summary>
    public override string ToString() => Value.ToString("D");
}
