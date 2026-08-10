#pragma warning disable CS1591

namespace MiniErp.Contracts.Modules.BusinessParties;

/// <summary>
/// Business Customer write payload. Tenant, company/branch scope,
/// permissions, approval, authentication and actor facts are intentionally
/// absent; the server derives those facts from the trusted request context.
/// This is an external B2B role, not a User or portal identity.
/// </summary>
public sealed record CustomerWriteRequest(
    string? Code,
    string? EnglishLegalName,
    string? ArabicLegalName,
    string? EnglishTradingName,
    string? ArabicTradingName,
    IReadOnlyList<CustomerContactWriteRequest>? Contacts);

/// <summary>
/// Named external Business Customer contact data. This is not a User,
/// credential, membership or authentication contract.
/// </summary>
public sealed record CustomerContactWriteRequest(
    string? Name,
    string? Email,
    string? Phone);

public sealed record CustomerLifecycleRequest(string? Reason = null);

public sealed record CustomerResponse(
    Guid Id,
    Guid TenantId,
    string Code,
    string? EnglishLegalName,
    string? ArabicLegalName,
    string? EnglishTradingName,
    string? ArabicTradingName,
    string LifecycleState,
    byte[] Version,
    IReadOnlyList<CustomerContactResponse> Contacts);

public sealed record CustomerContactResponse(
    Guid Id,
    Guid CustomerId,
    string Name,
    string? Email,
    string? Phone,
    byte[] Version);

public sealed record CustomerAuditResponse(
    Guid EvidenceId,
    DateTimeOffset OccurredAt,
    string OperationId,
    string CorrelationId,
    Guid TenantId,
    Guid ActorId,
    Guid SessionId,
    string AuthorizationPath,
    string Operation,
    string PolicyOutcome,
    string Decision,
    string Reason,
    string? BeforeSummary,
    string? AfterSummary,
    Guid? ApproverId);

#pragma warning restore CS1591
