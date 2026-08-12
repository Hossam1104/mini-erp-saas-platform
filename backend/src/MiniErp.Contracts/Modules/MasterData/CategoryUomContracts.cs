#pragma warning disable CS1591

namespace MiniErp.Contracts.Modules.MasterData;

public sealed record CategoryWriteRequest(
    string? Code,
    string? EnglishName,
    string? ArabicName,
    Guid? ParentCategoryId,
    bool TrackingDefaultEnabled);

public sealed record UnitOfMeasureWriteRequest(
    string? Code,
    string? EnglishName,
    string? ArabicName);

public sealed record MasterDataLifecycleRequest(string? Reason = null);

public sealed record CategoryResponse(
    Guid Id,
    Guid TenantId,
    string Code,
    string? EnglishName,
    string? ArabicName,
    Guid? ParentCategoryId,
    string LifecycleState,
    byte[] Version,
    bool TrackingDefaultEnabled);

public sealed record UnitOfMeasureResponse(
    Guid Id,
    Guid TenantId,
    string Code,
    string? EnglishName,
    string? ArabicName,
    string LifecycleState,
    byte[] Version);

public sealed record CategoryAuditResponse(
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
