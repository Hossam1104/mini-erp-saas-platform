#pragma warning disable CS1591

namespace MiniErp.Contracts.Modules.BusinessParties;

/// <summary>
/// Supplier write payload. Tenant, company/branch scope, permissions,
/// approval, authentication and actor facts are intentionally absent; the
/// server derives those facts from the trusted request context.
/// </summary>
public sealed record SupplierWriteRequest(
    string? Code,
    string? EnglishLegalName,
    string? ArabicLegalName,
    string? EnglishTradingName,
    string? ArabicTradingName,
    string? RegistrationReference,
    IReadOnlyList<SupplierContactWriteRequest>? Contacts);

/// <summary>
/// Named external Supplier contact data. This is not a User, credential, or
/// membership contract.
/// </summary>
public sealed record SupplierContactWriteRequest(
    string? Name,
    string? Email,
    string? Phone);

public sealed record SupplierLifecycleRequest(string? Reason = null);

public sealed record SupplierResponse(
    Guid Id,
    Guid TenantId,
    string Code,
    string? EnglishLegalName,
    string? ArabicLegalName,
    string? EnglishTradingName,
    string? ArabicTradingName,
    string? RegistrationReference,
    string LifecycleState,
    byte[] Version,
    IReadOnlyList<SupplierContactResponse> Contacts);

public sealed record SupplierContactResponse(
    Guid Id,
    Guid SupplierId,
    string Name,
    string? Email,
    string? Phone,
    byte[] Version);

public sealed record SupplierAuditResponse(
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
