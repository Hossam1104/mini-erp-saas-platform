using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.Infrastructure.Persistence.Modules.MasterData;

internal sealed class MasterDataCategoryEntity : ITenantOwned
{
    private MasterDataCategoryEntity()
    {
        Code = string.Empty;
        EnglishName = string.Empty;
        NameKey = string.Empty;
    }

    internal MasterDataCategoryEntity(
        Guid id,
        TenantId tenantId,
        string code,
        LocalizedName name,
        Guid? parentCategoryId)
    {
        Id = id;
        TenantId = tenantId;
        Code = code;
        EnglishName = name.English ?? string.Empty;
        ArabicName = name.Arabic;
        NameKey = (name.English ?? name.Arabic ?? string.Empty).ToUpperInvariant();
        ParentCategoryId = parentCategoryId;
        LifecycleState = MasterDataLifecycleState.Active;
    }

    internal Guid Id { get; private set; }

    public TenantId TenantId { get; private set; }

    internal string Code { get; private set; }

    internal string EnglishName { get; private set; }

    internal string? ArabicName { get; private set; }

    internal string NameKey { get; private set; }

    internal Guid? ParentCategoryId { get; private set; }

    internal MasterDataLifecycleState LifecycleState { get; private set; }

    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();

    internal LocalizedName Name => new(
        string.IsNullOrWhiteSpace(EnglishName) ? null : EnglishName,
        ArabicName);

    internal void Edit(string code, LocalizedName name, Guid? parentCategoryId)
    {
        Code = code;
        EnglishName = name.English ?? string.Empty;
        ArabicName = name.Arabic;
        NameKey = (name.English ?? name.Arabic ?? string.Empty).ToUpperInvariant();
        ParentCategoryId = parentCategoryId;
    }

    internal void SetLifecycle(MasterDataLifecycleState lifecycleState) => LifecycleState = lifecycleState;

    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class MasterDataUnitOfMeasureEntity : ITenantOwned
{
    private MasterDataUnitOfMeasureEntity()
    {
        Code = string.Empty;
        EnglishName = string.Empty;
        NameKey = string.Empty;
    }

    internal MasterDataUnitOfMeasureEntity(
        Guid id,
        TenantId tenantId,
        string code,
        LocalizedName name)
    {
        Id = id;
        TenantId = tenantId;
        Code = code;
        EnglishName = name.English ?? string.Empty;
        ArabicName = name.Arabic;
        NameKey = (name.English ?? name.Arabic ?? string.Empty).ToUpperInvariant();
        LifecycleState = MasterDataLifecycleState.Active;
    }

    internal Guid Id { get; private set; }

    public TenantId TenantId { get; private set; }

    internal string Code { get; private set; }

    internal string EnglishName { get; private set; }

    internal string? ArabicName { get; private set; }

    internal string NameKey { get; private set; }

    internal MasterDataLifecycleState LifecycleState { get; private set; }

    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();

    internal LocalizedName Name => new(
        string.IsNullOrWhiteSpace(EnglishName) ? null : EnglishName,
        ArabicName);

    internal void Edit(string code, LocalizedName name)
    {
        Code = code;
        EnglishName = name.English ?? string.Empty;
        ArabicName = name.Arabic;
        NameKey = (name.English ?? name.Arabic ?? string.Empty).ToUpperInvariant();
    }

    internal void SetLifecycle(MasterDataLifecycleState lifecycleState) => LifecycleState = lifecycleState;

    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class MasterDataConversionEntity : ITenantOwned
{
    private MasterDataConversionEntity()
    {
    }

    internal MasterDataConversionEntity(
        Guid id,
        TenantId tenantId,
        Guid fromUnitOfMeasureId,
        Guid toUnitOfMeasureId,
        decimal factor)
    {
        Id = id;
        TenantId = tenantId;
        FromUnitOfMeasureId = fromUnitOfMeasureId;
        ToUnitOfMeasureId = toUnitOfMeasureId;
        Factor = factor;
    }

    internal Guid Id { get; private set; }

    public TenantId TenantId { get; private set; }

    internal Guid FromUnitOfMeasureId { get; private set; }

    internal Guid ToUnitOfMeasureId { get; private set; }

    internal decimal Factor { get; private set; }

    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();
}

internal sealed class MasterDataAuditEventEntity : ITenantOwned
{
    private MasterDataAuditEventEntity()
    {
        OperationId = string.Empty;
        CorrelationId = string.Empty;
        BusinessCode = null;
        BeforeSummary = null;
        AfterSummary = null;
        ScopePolicyId = string.Empty;
    }

    internal MasterDataAuditEventEntity(MasterDataAuditEvidence evidence)
    {
        EvidenceId = evidence.EvidenceId;
        OccurredAt = evidence.OccurredAt;
        OperationId = evidence.OperationId;
        CorrelationId = evidence.CorrelationId;
        TenantId = new TenantId(evidence.Tenant.TenantId);
        ActorId = evidence.ActorId;
        SessionId = evidence.SessionId;
        AuthorizationPath = evidence.AuthorizationPath;
        ResourceKind = evidence.ResourceKind;
        ResourceId = evidence.ResourceId;
        BusinessCode = evidence.BusinessCode;
        Operation = evidence.Operation;
        PolicyOutcome = evidence.PolicyOutcome;
        Decision = evidence.Decision;
        Reason = evidence.Reason;
        BeforeSummary = evidence.BeforeSummary;
        AfterSummary = evidence.AfterSummary;
        ApproverId = evidence.ApproverId;
        ScopePolicyId = evidence.Scope?.Policy.PolicyId ?? string.Empty;
        ScopePolicyVersion = evidence.Scope?.Policy.Version ?? 0;
        ScopeAnchorKind = evidence.Scope?.OrganizationAnchor?.Kind;
        ScopeAnchorId = evidence.Scope?.OrganizationAnchor?.Id;
    }

    internal Guid EvidenceId { get; private set; }

    internal DateTimeOffset OccurredAt { get; private set; }

    internal string OperationId { get; private set; }

    internal string CorrelationId { get; private set; }

    public TenantId TenantId { get; private set; }

    internal Guid ActorId { get; private set; }

    internal Guid SessionId { get; private set; }

    internal FoundationAuditAuthorizationPath AuthorizationPath { get; private set; }

    internal MasterDataResourceKind ResourceKind { get; private set; }

    internal Guid? ResourceId { get; private set; }

    internal string? BusinessCode { get; private set; }

    internal MasterDataOperation Operation { get; private set; }

    internal MasterDataPolicyOutcome PolicyOutcome { get; private set; }

    internal FoundationAuditDecision Decision { get; private set; }

    internal FoundationAuditReason Reason { get; private set; }

    internal string? BeforeSummary { get; private set; }

    internal string? AfterSummary { get; private set; }

    internal Guid? ApproverId { get; private set; }

    internal string ScopePolicyId { get; private set; }

    internal int ScopePolicyVersion { get; private set; }

    internal OrganizationScopeKind? ScopeAnchorKind { get; private set; }

    internal Guid? ScopeAnchorId { get; private set; }

    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();
}
