#pragma warning disable CS1591

using System.Collections.Frozen;
using MiniErp.App.BuildingBlocks.Tenancy;

namespace MiniErp.App.BuildingBlocks.Work;

/// <summary>
/// Defines how a registered durable operation may use a verified organization
/// scope.
/// </summary>
public enum DurableWorkScopePolicy
{
    ExactRequestedScope = 1,
    TenantWideOnly = 2
}

/// <summary>
/// Immutable server-owned descriptor for one durable operation. The permission
/// is part of this descriptor and cannot be supplied independently by a work
/// caller.
/// </summary>
public sealed class DurableWorkOperationDescriptor
{
    public DurableWorkOperationDescriptor(
        string operationId,
        string workType,
        string exactPermissionCode,
        IEnumerable<TenantAuthorizationPath> allowedAuthorizationPaths,
        DurableWorkScopePolicy scopePolicy = DurableWorkScopePolicy.ExactRequestedScope,
        bool requiresCurrentSession = true)
    {
        OperationId = Required(operationId, nameof(operationId));
        WorkType = Required(workType, nameof(workType));
        ExactPermissionCode = Required(exactPermissionCode, nameof(exactPermissionCode)).ToLowerInvariant();
        if (!Enum.IsDefined(scopePolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(scopePolicy));
        }

        ArgumentNullException.ThrowIfNull(allowedAuthorizationPaths);
        var paths = allowedAuthorizationPaths
            .Distinct()
            .ToArray();
        if (paths.Length == 0 || paths.Any(path => !Enum.IsDefined(path)))
        {
            throw new ArgumentException(
                "A durable operation must declare at least one valid authorization path.",
                nameof(allowedAuthorizationPaths));
        }

        OperationId = OperationId.Trim();
        WorkType = WorkType.Trim();
        ExactPermissionCode = ExactPermissionCode.Trim();
        AllowedAuthorizationPaths = paths.ToFrozenSet();
        ScopePolicy = scopePolicy;
        RequiresCurrentSession = requiresCurrentSession;
    }

    public string OperationId { get; }

    public string WorkType { get; }

    public string ExactPermissionCode { get; }

    public IReadOnlySet<TenantAuthorizationPath> AllowedAuthorizationPaths { get; }

    public DurableWorkScopePolicy ScopePolicy { get; }

    public bool RequiresCurrentSession { get; }

    internal bool ExactlyMatches(DurableWorkOperationDescriptor other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return string.Equals(OperationId, other.OperationId, StringComparison.Ordinal)
            && string.Equals(WorkType, other.WorkType, StringComparison.Ordinal)
            && string.Equals(ExactPermissionCode, other.ExactPermissionCode, StringComparison.Ordinal)
            && ScopePolicy == other.ScopePolicy
            && RequiresCurrentSession == other.RequiresCurrentSession
            && AllowedAuthorizationPaths.SetEquals(other.AllowedAuthorizationPaths);
    }

    private static string Required(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 128)
        {
            throw new ArgumentException("The operation descriptor value is required and bounded.", name);
        }

        var normalized = value.Trim();
        if (normalized.Any(char.IsControl))
        {
            throw new ArgumentException("The operation descriptor value contains a control character.", name);
        }

        return normalized;
    }
}

/// <summary>Authoritative operation lookup used by submission and dispatch.</summary>
public interface IDurableWorkOperationCatalogue
{
    bool TryGet(string operationId, out DurableWorkOperationDescriptor descriptor);
}

/// <summary>
/// Bounded immutable catalogue. A live implementation can later be backed by
/// approved configuration; this Foundation adapter has no database or provider
/// behavior.
/// </summary>
public sealed class DurableWorkOperationCatalogue : IDurableWorkOperationCatalogue
{
    private readonly IReadOnlyDictionary<string, DurableWorkOperationDescriptor> descriptors;

    public DurableWorkOperationCatalogue(IEnumerable<DurableWorkOperationDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        var entries = new Dictionary<string, DurableWorkOperationDescriptor>(StringComparer.Ordinal);
        foreach (var descriptor in descriptors)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            if (!entries.TryAdd(descriptor.OperationId, descriptor))
            {
                throw new ArgumentException(
                    "An operation descriptor may be registered only once.",
                    nameof(descriptors));
            }
        }

        this.descriptors = entries.ToFrozenDictionary(StringComparer.Ordinal);
    }

    public static DurableWorkOperationCatalogue Empty { get; } =
        new(Array.Empty<DurableWorkOperationDescriptor>());

    public bool TryGet(string operationId, out DurableWorkOperationDescriptor descriptor)
    {
        descriptor = null!;
        if (string.IsNullOrWhiteSpace(operationId))
        {
            return false;
        }

        return descriptors.TryGetValue(operationId.Trim(), out descriptor!);
    }
}

#pragma warning restore CS1591
