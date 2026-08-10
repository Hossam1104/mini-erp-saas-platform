#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.Infrastructure.Persistence.Modules.BusinessParties;

internal sealed class BusinessPartiesSupplierEntity : ITenantOwned
{
    private BusinessPartiesSupplierEntity()
    {
        Code = string.Empty;
        CodeKey = string.Empty;
        EnglishLegalName = string.Empty;
        EnglishLegalNameKey = string.Empty;
        Contacts = new List<BusinessPartiesSupplierContactEntity>();
    }

    internal BusinessPartiesSupplierEntity(
        Guid id,
        TenantId tenantId,
        string code,
        LocalizedName legalName,
        LocalizedName? tradingName,
        string? registrationReference)
    {
        Id = id;
        TenantId = tenantId;
        Code = code;
        CodeKey = code.ToUpperInvariant();
        SetNames(legalName, tradingName);
        SetRegistrationReference(registrationReference);
        LifecycleState = MasterDataLifecycleState.Active;
        Contacts = new List<BusinessPartiesSupplierContactEntity>();
    }

    internal Guid Id { get; private set; }

    public TenantId TenantId { get; private set; }

    internal string Code { get; private set; }

    internal string CodeKey { get; private set; }

    internal string EnglishLegalName { get; private set; } = string.Empty;

    internal string? ArabicLegalName { get; private set; }

    internal string EnglishLegalNameKey { get; private set; } = string.Empty;

    internal string? ArabicLegalNameKey { get; private set; }

    internal string? EnglishTradingName { get; private set; }

    internal string? ArabicTradingName { get; private set; }

    internal string? EnglishTradingNameKey { get; private set; }

    internal string? ArabicTradingNameKey { get; private set; }

    internal string? RegistrationReference { get; private set; }

    internal string? RegistrationKey { get; private set; }

    internal MasterDataLifecycleState LifecycleState { get; private set; }

    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();

    internal ICollection<BusinessPartiesSupplierContactEntity> Contacts { get; private set; }

    internal LocalizedName LegalName => new(EnglishLegalName, ArabicLegalName);

    internal LocalizedName? TradingName =>
        EnglishTradingName is null && ArabicTradingName is null
            ? null
            : new LocalizedName(EnglishTradingName, ArabicTradingName);

    internal void Edit(
        string code,
        LocalizedName legalName,
        LocalizedName? tradingName,
        string? registrationReference)
    {
        Code = code;
        CodeKey = code.ToUpperInvariant();
        SetNames(legalName, tradingName);
        SetRegistrationReference(registrationReference);
    }

    internal void SetLifecycle(MasterDataLifecycleState lifecycleState) => LifecycleState = lifecycleState;

    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();

    private void SetNames(LocalizedName legalName, LocalizedName? tradingName)
    {
        EnglishLegalName = legalName.English ?? string.Empty;
        ArabicLegalName = legalName.Arabic;
        EnglishLegalNameKey = ToKey(EnglishLegalName);
        ArabicLegalNameKey = ToOptionalKey(ArabicLegalName);
        EnglishTradingName = tradingName?.English;
        ArabicTradingName = tradingName?.Arabic;
        EnglishTradingNameKey = ToOptionalKey(EnglishTradingName);
        ArabicTradingNameKey = ToOptionalKey(ArabicTradingName);
    }

    private void SetRegistrationReference(string? registrationReference)
    {
        RegistrationReference = registrationReference;
        RegistrationKey = ToOptionalKey(registrationReference);
    }

    private static string ToKey(string value) => value.Trim().ToUpperInvariant();

    private static string? ToOptionalKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : ToKey(value);
}

internal sealed class BusinessPartiesSupplierContactEntity : ITenantOwned
{
    private BusinessPartiesSupplierContactEntity()
    {
        Name = string.Empty;
    }

    internal BusinessPartiesSupplierContactEntity(
        Guid id,
        TenantId tenantId,
        Guid supplierId,
        string name,
        string? email,
        string? phone)
    {
        Id = id;
        TenantId = tenantId;
        SupplierId = supplierId;
        Name = name;
        Email = email;
        Phone = phone;
    }

    internal Guid Id { get; private set; }

    public TenantId TenantId { get; private set; }

    internal Guid SupplierId { get; private set; }

    internal string Name { get; private set; }

    internal string? Email { get; private set; }

    internal string? Phone { get; private set; }

    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();
}

internal sealed class BusinessPartiesCustomerEntity : ITenantOwned
{
    private BusinessPartiesCustomerEntity()
    {
        Code = string.Empty;
        CodeKey = string.Empty;
        EnglishLegalName = string.Empty;
        EnglishLegalNameKey = string.Empty;
        Contacts = new List<BusinessPartiesCustomerContactEntity>();
    }

    internal BusinessPartiesCustomerEntity(
        Guid id,
        TenantId tenantId,
        string code,
        LocalizedName legalName,
        LocalizedName? tradingName)
    {
        Id = id;
        TenantId = tenantId;
        Code = code;
        CodeKey = code.ToUpperInvariant();
        SetNames(legalName, tradingName);
        LifecycleState = MasterDataLifecycleState.Active;
        Contacts = new List<BusinessPartiesCustomerContactEntity>();
    }

    internal Guid Id { get; private set; }

    public TenantId TenantId { get; private set; }

    internal string Code { get; private set; }

    internal string CodeKey { get; private set; }

    internal string EnglishLegalName { get; private set; } = string.Empty;

    internal string? ArabicLegalName { get; private set; }

    internal string EnglishLegalNameKey { get; private set; } = string.Empty;

    internal string? ArabicLegalNameKey { get; private set; }

    internal string? EnglishTradingName { get; private set; }

    internal string? ArabicTradingName { get; private set; }

    internal string? EnglishTradingNameKey { get; private set; }

    internal string? ArabicTradingNameKey { get; private set; }

    internal MasterDataLifecycleState LifecycleState { get; private set; }

    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();

    internal ICollection<BusinessPartiesCustomerContactEntity> Contacts { get; private set; }

    internal LocalizedName LegalName => new(EnglishLegalName, ArabicLegalName);

    internal LocalizedName? TradingName =>
        EnglishTradingName is null && ArabicTradingName is null
            ? null
            : new LocalizedName(EnglishTradingName, ArabicTradingName);

    internal void Edit(
        string code,
        LocalizedName legalName,
        LocalizedName? tradingName)
    {
        Code = code;
        CodeKey = code.ToUpperInvariant();
        SetNames(legalName, tradingName);
    }

    internal void SetLifecycle(MasterDataLifecycleState lifecycleState) => LifecycleState = lifecycleState;

    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();

    private void SetNames(LocalizedName legalName, LocalizedName? tradingName)
    {
        EnglishLegalName = legalName.English ?? string.Empty;
        ArabicLegalName = legalName.Arabic;
        EnglishLegalNameKey = ToKey(EnglishLegalName);
        ArabicLegalNameKey = ToOptionalKey(ArabicLegalName);
        EnglishTradingName = tradingName?.English;
        ArabicTradingName = tradingName?.Arabic;
        EnglishTradingNameKey = ToOptionalKey(EnglishTradingName);
        ArabicTradingNameKey = ToOptionalKey(ArabicTradingName);
    }

    private static string ToKey(string value) => value.Trim().ToUpperInvariant();

    private static string? ToOptionalKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : ToKey(value);
}

internal sealed class BusinessPartiesCustomerContactEntity : ITenantOwned
{
    private BusinessPartiesCustomerContactEntity()
    {
        Name = string.Empty;
    }

    internal BusinessPartiesCustomerContactEntity(
        Guid id,
        TenantId tenantId,
        Guid customerId,
        string name,
        string? email,
        string? phone)
    {
        Id = id;
        TenantId = tenantId;
        CustomerId = customerId;
        Name = name;
        Email = email;
        Phone = phone;
    }

    internal Guid Id { get; private set; }

    public TenantId TenantId { get; private set; }

    internal Guid CustomerId { get; private set; }

    internal string Name { get; private set; }

    internal string? Email { get; private set; }

    internal string? Phone { get; private set; }

    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();
}

internal sealed class BusinessPartiesAuditEventEntity : ITenantOwned
{
    private BusinessPartiesAuditEventEntity()
    {
        OperationId = string.Empty;
        CorrelationId = string.Empty;
        ScopePolicyId = string.Empty;
    }

    internal BusinessPartiesAuditEventEntity(MasterDataAuditEvidence evidence)
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

#pragma warning restore CS1591
