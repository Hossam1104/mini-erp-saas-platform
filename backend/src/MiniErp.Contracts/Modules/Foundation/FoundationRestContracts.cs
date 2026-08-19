namespace MiniErp.Contracts.Modules.Foundation;

/// <summary>
/// The public security profiles used by the Foundation REST contract.
/// Each public operation selects exactly one profile.
/// </summary>
public enum FoundationSecurityProfile
{
    /// <summary>Anonymous operation with no authorization context.</summary>
    Anonymous = 1,
    /// <summary>Authenticated session without a Tenant or platform path.</summary>
    AuthenticatedSession = 2,
    /// <summary>Active ordinary Tenant membership.</summary>
    OrdinaryMembership = 3,
    /// <summary>Case-bound support access for one Tenant.</summary>
    SupportGrant = 4,
    /// <summary>Purpose-bound Platform governance context.</summary>
    PlatformGovernanceContext = 5,
    /// <summary>High-risk operation requiring additional assurance.</summary>
    HighRisk = 6
}

/// <summary>
/// Visibility of an operation in the public contract catalogue.
/// </summary>
public enum FoundationOperationVisibility
{
    /// <summary>Included in the public REST contract.</summary>
    Public = 1,
    /// <summary>Internal application operation, never mapped publicly.</summary>
    Internal = 2
}

/// <summary>
/// The server-owned authorization scope required by a Foundation operation.
/// </summary>
public enum FoundationScopePolicy
{
    /// <summary>No business authorization scope is selected.</summary>
    None = 1,
    /// <summary>An active ordinary Tenant membership and organization scope.</summary>
    Tenant = 2,
    /// <summary>An active case-bound support grant and organization scope.</summary>
    SupportGrant = 3,
    /// <summary>A purpose-bound platform governance context.</summary>
    PlatformGovernance = 4
}

/// <summary>Concurrency evidence expected by a public operation.</summary>
public enum FoundationConcurrencyPolicy
{
    /// <summary>No optimistic-concurrency header is required.</summary>
    None = 1,
    /// <summary>The current entity version must be supplied in If-Match.</summary>
    IfMatch = 2
}

/// <summary>Idempotency evidence expected by a public operation.</summary>
public enum FoundationIdempotencyPolicy
{
    /// <summary>No idempotency key is required.</summary>
    None = 1,
    /// <summary>The mutation requires a server-bound Idempotency-Key.</summary>
    Required = 2
}

/// <summary>Effective-date contract expected by a public operation.</summary>
public enum FoundationEffectiveDatePolicy
{
    /// <summary>The operation does not select an effective date.</summary>
    None = 1,
    /// <summary>The operation requires an effective date query parameter.</summary>
    QueryRequired = 2,
    /// <summary>The request body must carry an effective date.</summary>
    RequestRequired = 3
}

/// <summary>
/// Stable mapping between one versioned endpoint and one application operation.
/// </summary>
public sealed record FoundationOperationDescriptor(
    string OperationId,
    string Route,
    string HttpMethod,
    FoundationSecurityProfile SecurityProfile,
    FoundationOperationVisibility Visibility,
    string? ExactPermissionCode = null,
    FoundationScopePolicy ScopePolicy = FoundationScopePolicy.None,
    bool RequiresMfa = false,
    bool RequiresFreshAuthentication = false,
    bool RequiresAntiforgery = false,
    bool RequiresMandatoryAudit = false,
    bool IsUnsafe = false,
    FoundationConcurrencyPolicy Concurrency = FoundationConcurrencyPolicy.None,
    FoundationIdempotencyPolicy Idempotency = FoundationIdempotencyPolicy.None,
    FoundationEffectiveDatePolicy EffectiveDate = FoundationEffectiveDatePolicy.None);

/// <summary>
/// Metadata attached to every public endpoint.
/// </summary>
public sealed class FoundationOperationMetadata
{
    /// <summary>Creates metadata directly from the approved descriptor.</summary>
    public FoundationOperationMetadata(FoundationOperationDescriptor descriptor)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    }

    // Compatibility adapter for isolated endpoint tests. Production endpoint
    // registration uses the catalog-backed constructor above.
    /// <summary>Creates metadata after verifying the supplied values match the catalogue.</summary>
    public FoundationOperationMetadata(
        string operationId,
        FoundationSecurityProfile securityProfile,
        FoundationOperationVisibility visibility = FoundationOperationVisibility.Public,
        bool RequiresMandatoryAudit = false,
        bool IsUnsafe = false)
        : this(FoundationOperationCatalog.GetRequired(operationId))
    {
        if (Descriptor.SecurityProfile != securityProfile
            || Descriptor.Visibility != visibility
            || Descriptor.RequiresMandatoryAudit != RequiresMandatoryAudit
            || Descriptor.IsUnsafe != IsUnsafe)
        {
            throw new ArgumentException("Endpoint metadata must match the approved operation descriptor.", nameof(operationId));
        }
    }

    /// <summary>The authoritative operation descriptor.</summary>
    public FoundationOperationDescriptor Descriptor { get; }

    /// <summary>The stable operation identifier.</summary>
    public string OperationId => Descriptor.OperationId;

    /// <summary>The operation security profile.</summary>
    public FoundationSecurityProfile SecurityProfile => Descriptor.SecurityProfile;

    /// <summary>The operation visibility.</summary>
    public FoundationOperationVisibility Visibility => Descriptor.Visibility;

    /// <summary>The exact permission code, when the operation has one.</summary>
    public string? ExactPermissionCode => Descriptor.ExactPermissionCode;

    /// <summary>The operation scope policy.</summary>
    public FoundationScopePolicy ScopePolicy => Descriptor.ScopePolicy;

    /// <summary>Whether MFA is required.</summary>
    public bool RequiresMfa => Descriptor.RequiresMfa;

    /// <summary>Whether fresh authentication is required.</summary>
    public bool RequiresFreshAuthentication => Descriptor.RequiresFreshAuthentication;

    /// <summary>Whether ASP.NET Core antiforgery is required.</summary>
    public bool RequiresAntiforgery => Descriptor.RequiresAntiforgery;

    /// <summary>Whether mandatory evidence is required.</summary>
    public bool RequiresMandatoryAudit => Descriptor.RequiresMandatoryAudit;

    /// <summary>Whether the operation is unsafe.</summary>
    public bool IsUnsafe => Descriptor.IsUnsafe;

    /// <summary>The required optimistic-concurrency contract.</summary>
    public FoundationConcurrencyPolicy Concurrency => Descriptor.Concurrency;

    /// <summary>The required idempotency contract.</summary>
    public FoundationIdempotencyPolicy Idempotency => Descriptor.Idempotency;

    /// <summary>The effective-date contract.</summary>
    public FoundationEffectiveDatePolicy EffectiveDate => Descriptor.EffectiveDate;
}

/// <summary>Safe first-party authentication request.</summary>
public sealed record FoundationSignInRequest(string? Login, string? Password);

/// <summary>Safe session summary returned to the first-party shell.</summary>
public sealed record FoundationSessionResponse(
    bool Authenticated,
    Guid? ActorId,
    Guid? SessionId,
    string LifecycleState,
    DateTimeOffset? AbsoluteExpiresAt,
    string? SelectedPath,
    Guid? SelectedTenantId,
    Guid? SelectedContextId,
    long SelectionVersion,
    bool Replayed = false);

/// <summary>One safe server-derived context candidate.</summary>
public sealed record FoundationContextCandidateResponse(
    Guid ContextId,
    string Kind,
    Guid? TenantId,
    string DisplayName,
    long EligibilityVersion);

/// <summary>Authorized-context list response.</summary>
public sealed record FoundationContextsResponse(
    IReadOnlyList<FoundationContextCandidateResponse> Contexts);

/// <summary>One safe Tenant candidate returned by the entry-routing seam.</summary>
public sealed record FoundationTenantCandidateResponse(
    Guid TenantId,
    string DisplayName,
    string? CanonicalHost);

/// <summary>One safe Company/Branch operational context candidate.</summary>
public sealed record FoundationOperationalContextResponse(
    Guid ContextId,
    string Kind,
    string DisplayName,
    long EligibilityVersion);

/// <summary>Safe tenant-aware branding configuration.</summary>
public sealed record FoundationBrandingResponse(
    string DisplayName,
    string? LogoLightUrl,
    string? LogoDarkUrl,
    string LogoAltText,
    bool TenantConfigured);

/// <summary>Presentation-only currency symbol configuration.</summary>
public sealed record FoundationCurrencyPresentationResponse(
    string CurrencyCode,
    string? SymbolAssetUrl,
    string SymbolTextFallback);

/// <summary>
/// Server-owned entry resolution returned after authentication. The response
/// contains only authorized candidates and safe presentation data; it never
/// grants authority to a client-supplied Tenant identifier.
/// </summary>
public sealed record FoundationEntryResponse(
    string EntryMode,
    string? CanonicalHost,
    Guid? CandidateTenantId,
    string? CandidateTenantDisplayName,
    IReadOnlyList<FoundationTenantCandidateResponse> AuthorizedTenants,
    IReadOnlyList<FoundationOperationalContextResponse> OperationalContexts,
    Guid? SelectedOperationalContextId,
    long OperationalSelectionVersion,
    FoundationBrandingResponse Branding,
    FoundationCurrencyPresentationResponse CurrencyPresentation,
    string? Code = null);

/// <summary>Authorized operational-context list response.</summary>
public sealed record FoundationOperationalContextsResponse(
    IReadOnlyList<FoundationOperationalContextResponse> Contexts,
    Guid? SelectedContextId,
    long SelectionVersion);

/// <summary>Server-confirmed context switch request.</summary>
public sealed record FoundationContextSwitchRequest(
    Guid ContextId,
    long ExpectedSelectionVersion = 0,
    long ExpectedEligibilityVersion = 0);

/// <summary>Server-confirmed Company/Branch context switch request.</summary>
public sealed record FoundationOperationalContextSwitchRequest(
    Guid ContextId,
    long ExpectedSelectionVersion = 0,
    long ExpectedEligibilityVersion = 0);

/// <summary>Result of selecting one operational context.</summary>
public sealed record FoundationOperationalContextSwitchResponse(
    FoundationOperationalContextResponse SelectedContext,
    long SelectionVersion);

/// <summary>
/// Stable, safe response for the representative Foundation context operation.
/// </summary>
public sealed record FoundationContextResponse(
    string OperationId,
    string CorrelationId,
    string AuthorizationPath,
    string LifecycleState,
    string Permission,
    string? TenantId,
    string? Scope,
    bool PlatformGovernance);

/// <summary>
/// Stable, safe response for the representative Foundation write operation.
/// </summary>
public sealed record FoundationWriteResponse(
    string OperationId,
    string CorrelationId,
    string Result,
    string Version,
    bool Replayed);

/// <summary>Typed safe idempotency response; replay never rebuilds mutable state.</summary>
public sealed record FoundationIdempotencyResponse(
    FoundationWriteResponse? WriteResponse = null,
    FoundationSessionResponse? SessionResponse = null)
{
    /// <summary>Wraps a write response for idempotent storage.</summary>
    public static FoundationIdempotencyResponse ForWrite(FoundationWriteResponse response) =>
        new(response ?? throw new ArgumentNullException(nameof(response)), null);

    /// <summary>Wraps a session response for idempotent storage.</summary>
    public static FoundationIdempotencyResponse ForSession(FoundationSessionResponse response) =>
        new(null, response ?? throw new ArgumentNullException(nameof(response)));
}

/// <summary>
/// The allow-listed request body for the non-business Foundation write probe.
/// </summary>
public sealed record FoundationWriteRequest(
    string? Value,
    Guid? TargetId = null);

/// <summary>
/// Stable, safe response for the target-existence demonstration.
/// </summary>
public sealed record FoundationTargetResponse(
    string OperationId,
    string CorrelationId,
    string Result,
    string TargetId);

/// <summary>
/// The single public operation catalogue for the Foundation seam.
/// </summary>
public static class FoundationOperationCatalog
{
    /// <summary>The complete public operation catalogue.</summary>
    public static IReadOnlyList<FoundationOperationDescriptor> PublicOperations { get; } =
    [
        new("platform.health", "/health", "GET", FoundationSecurityProfile.Anonymous, FoundationOperationVisibility.Public),
        new("platform.openapi", "/openapi/v1.json", "GET", FoundationSecurityProfile.Anonymous, FoundationOperationVisibility.Public),
        new("platform.module-registration", "/api/v1/module-registration", "GET", FoundationSecurityProfile.Anonymous, FoundationOperationVisibility.Public),
        new("foundation.tenant-context.read", "/api/v1/foundation/tenant-context", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.foundation.context.read", FoundationScopePolicy.Tenant),
        new("foundation.support-context.read", "/api/v1/foundation/support-context", "GET", FoundationSecurityProfile.SupportGrant, FoundationOperationVisibility.Public, "support.tenant.read", FoundationScopePolicy.SupportGrant, RequiresMfa: true, RequiresFreshAuthentication: true),
        new("foundation.platform-context.read", "/api/v1/foundation/platform-context", "GET", FoundationSecurityProfile.PlatformGovernanceContext, FoundationOperationVisibility.Public, "platform.metadata.read", FoundationScopePolicy.PlatformGovernance, RequiresMfa: true, RequiresFreshAuthentication: true),
        new("foundation.target.read", "/api/v1/foundation/targets/{targetId}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.foundation.target.read", FoundationScopePolicy.Tenant),
        new("foundation.probe.write", "/api/v1/foundation/probe", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.foundation.probe.write", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.category.list", "/api/v1/master-data/categories", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.category.view", FoundationScopePolicy.Tenant),
        new("master-data.category.read", "/api/v1/master-data/categories/{categoryId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.category.view", FoundationScopePolicy.Tenant),
        new("master-data.category.create", "/api/v1/master-data/categories", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.category.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.category.edit", "/api/v1/master-data/categories/{categoryId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.category.edit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.category.deactivate", "/api/v1/master-data/categories/{categoryId:guid}/deactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.category.deactivate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.category.reactivate", "/api/v1/master-data/categories/{categoryId:guid}/reactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.category.activate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.category.audit.read", "/api/v1/master-data/categories/{categoryId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.category.audit", FoundationScopePolicy.Tenant),
        new("master-data.unit.list", "/api/v1/master-data/units-of-measure", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.unit.view", FoundationScopePolicy.Tenant),
        new("master-data.unit.read", "/api/v1/master-data/units-of-measure/{unitId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.unit.view", FoundationScopePolicy.Tenant),
        new("master-data.unit.create", "/api/v1/master-data/units-of-measure", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.unit.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.unit.edit", "/api/v1/master-data/units-of-measure/{unitId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.unit.edit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.unit.deactivate", "/api/v1/master-data/units-of-measure/{unitId:guid}/deactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.unit.deactivate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.unit.reactivate", "/api/v1/master-data/units-of-measure/{unitId:guid}/reactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.unit.activate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.unit.audit.read", "/api/v1/master-data/units-of-measure/{unitId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.unit.audit", FoundationScopePolicy.Tenant),
        new("master-data.product.list", "/api/v1/master-data/products", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.product.view", FoundationScopePolicy.Tenant),
        new("master-data.product.read", "/api/v1/master-data/products/{productId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.product.view", FoundationScopePolicy.Tenant),
        new("master-data.product.create", "/api/v1/master-data/products", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.product.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.product.edit", "/api/v1/master-data/products/{productId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.product.edit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.product.deactivate", "/api/v1/master-data/products/{productId:guid}/deactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.product.deactivate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.product.reactivate", "/api/v1/master-data/products/{productId:guid}/reactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.product.activate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.product.audit.read", "/api/v1/master-data/products/{productId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.product.audit", FoundationScopePolicy.Tenant),
        new("master-data.supplier.list", "/api/v1/master-data/suppliers", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.supplier.view", FoundationScopePolicy.Tenant),
        new("master-data.supplier.read", "/api/v1/master-data/suppliers/{supplierId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.supplier.view", FoundationScopePolicy.Tenant),
        new("master-data.supplier.create", "/api/v1/master-data/suppliers", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.supplier.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.supplier.edit", "/api/v1/master-data/suppliers/{supplierId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.supplier.edit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.supplier.deactivate", "/api/v1/master-data/suppliers/{supplierId:guid}/deactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.supplier.deactivate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.supplier.reactivate", "/api/v1/master-data/suppliers/{supplierId:guid}/reactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.supplier.activate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.supplier.audit.read", "/api/v1/master-data/suppliers/{supplierId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.supplier.audit", FoundationScopePolicy.Tenant),
        new("master-data.customer.list", "/api/v1/master-data/customers", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.customer.view", FoundationScopePolicy.Tenant),
        new("master-data.customer.read", "/api/v1/master-data/customers/{customerId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.customer.view", FoundationScopePolicy.Tenant),
        new("master-data.customer.create", "/api/v1/master-data/customers", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.customer.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.customer.edit", "/api/v1/master-data/customers/{customerId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.customer.edit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.customer.deactivate", "/api/v1/master-data/customers/{customerId:guid}/deactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.customer.deactivate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.customer.reactivate", "/api/v1/master-data/customers/{customerId:guid}/reactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.customer.activate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.customer.audit.read", "/api/v1/master-data/customers/{customerId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.customer.audit", FoundationScopePolicy.Tenant),
        new("master-data.currency.list", "/api/v1/master-data/currencies", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.currency.view", FoundationScopePolicy.Tenant),
        new("master-data.currency.read", "/api/v1/master-data/currencies/{currencyId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.currency.view", FoundationScopePolicy.Tenant),
        new("master-data.currency.create", "/api/v1/master-data/currencies", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.currency.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.currency.edit", "/api/v1/master-data/currencies/{currencyId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.currency.edit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.currency.deactivate", "/api/v1/master-data/currencies/{currencyId:guid}/deactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.currency.deactivate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.currency.reactivate", "/api/v1/master-data/currencies/{currencyId:guid}/reactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.currency.activate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.currency.audit.read", "/api/v1/master-data/currencies/{currencyId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.currency.audit", FoundationScopePolicy.Tenant),
        new("master-data.currency.reference.read", "/api/v1/master-data/currencies/{currencyId:guid}/reference", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.currency.view", FoundationScopePolicy.Tenant),
        new("master-data.payment-term.list", "/api/v1/master-data/payment-terms", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.payment-term.view", FoundationScopePolicy.Tenant),
        new("master-data.payment-term.read", "/api/v1/master-data/payment-terms/{paymentTermId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.payment-term.view", FoundationScopePolicy.Tenant),
        new("master-data.payment-term.create", "/api/v1/master-data/payment-terms", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.payment-term.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.payment-term.edit", "/api/v1/master-data/payment-terms/{paymentTermId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.payment-term.edit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.payment-term.deactivate", "/api/v1/master-data/payment-terms/{paymentTermId:guid}/deactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.payment-term.deactivate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.payment-term.reactivate", "/api/v1/master-data/payment-terms/{paymentTermId:guid}/reactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.payment-term.activate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.payment-term.audit.read", "/api/v1/master-data/payment-terms/{paymentTermId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.payment-term.audit", FoundationScopePolicy.Tenant),
        new("master-data.payment-term.history.read", "/api/v1/master-data/payment-terms/{paymentTermId:guid}/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.payment-term.view", FoundationScopePolicy.Tenant),
        new("master-data.payment-term.reference.read", "/api/v1/master-data/payment-terms/{paymentTermId:guid}/reference", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.payment-term.view", FoundationScopePolicy.Tenant),
        new("master-data.payment-term.preview.read", "/api/v1/master-data/payment-terms/{paymentTermId:guid}/due-date-preview", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.payment-term.view", FoundationScopePolicy.Tenant),
        new("master-data.tax.list", "/api/v1/master-data/taxes", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.tax.view", FoundationScopePolicy.Tenant),
        new("master-data.tax.read", "/api/v1/master-data/taxes/{taxId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.tax.view", FoundationScopePolicy.Tenant),
        new("master-data.tax.history.read", "/api/v1/master-data/taxes/{taxId:guid}/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.tax.view", FoundationScopePolicy.Tenant),
        new("master-data.tax.reference.read", "/api/v1/master-data/taxes/{taxId:guid}/reference", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.tax.view", FoundationScopePolicy.Tenant),
        new("master-data.tax.calculate", "/api/v1/master-data/taxes/{taxId:guid}/calculate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.tax.view", FoundationScopePolicy.Tenant),
        new("master-data.tax.create", "/api/v1/master-data/taxes", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.tax.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.tax.edit", "/api/v1/master-data/taxes/{taxId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.tax.edit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.tax.deactivate", "/api/v1/master-data/taxes/{taxId:guid}/deactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.tax.deactivate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.tax.reactivate", "/api/v1/master-data/taxes/{taxId:guid}/reactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.tax.activate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.tax.audit.read", "/api/v1/master-data/taxes/{taxId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.tax.audit", FoundationScopePolicy.Tenant),
        new("master-data.exchange-rate.list", "/api/v1/master-data/exchange-rates", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.exchange-rate.view", FoundationScopePolicy.Tenant),
        new("master-data.exchange-rate.read", "/api/v1/master-data/exchange-rates/{exchangeRateId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.exchange-rate.view", FoundationScopePolicy.Tenant),
        new("master-data.exchange-rate.history.read", "/api/v1/master-data/exchange-rates/{exchangeRateId:guid}/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.exchange-rate.view", FoundationScopePolicy.Tenant),
        new("master-data.exchange-rate.reference.read", "/api/v1/master-data/exchange-rates/{exchangeRateId:guid}/reference", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.exchange-rate.view", FoundationScopePolicy.Tenant),
        new("master-data.exchange-rate.create", "/api/v1/master-data/exchange-rates", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.exchange-rate.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.exchange-rate.edit", "/api/v1/master-data/exchange-rates/{exchangeRateId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.exchange-rate.edit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.exchange-rate.deactivate", "/api/v1/master-data/exchange-rates/{exchangeRateId:guid}/deactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.exchange-rate.deactivate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.exchange-rate.reactivate", "/api/v1/master-data/exchange-rates/{exchangeRateId:guid}/reactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.exchange-rate.activate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("master-data.exchange-rate.audit.read", "/api/v1/master-data/exchange-rates/{exchangeRateId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.exchange-rate.audit", FoundationScopePolicy.Tenant),
        new("master-data.price-list.list", "/api/v1/master-data/price-lists", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.price-list.view", FoundationScopePolicy.Tenant),
        new("master-data.price-list.read", "/api/v1/master-data/price-lists/{priceListId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.price-list.view", FoundationScopePolicy.Tenant),
        new("master-data.price-list.history.read", "/api/v1/master-data/price-lists/{priceListId:guid}/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.price-list.view", FoundationScopePolicy.Tenant),
        new("master-data.price-list.create", "/api/v1/master-data/price-lists", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.price-list.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("master-data.price-list.edit", "/api/v1/master-data/price-lists/{priceListId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.price-list.edit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("master-data.price-list.price.append", "/api/v1/master-data/price-lists/{priceListId:guid}/prices", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.price-list.edit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required, EffectiveDate: FoundationEffectiveDatePolicy.RequestRequired),
        new("master-data.price-list.deactivate", "/api/v1/master-data/price-lists/{priceListId:guid}/deactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.price-list.deactivate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("master-data.price-list.reactivate", "/api/v1/master-data/price-lists/{priceListId:guid}/reactivate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.price-list.activate", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("master-data.price-list.reference.read", "/api/v1/master-data/price-lists/reference", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.price-list.view", FoundationScopePolicy.Tenant, EffectiveDate: FoundationEffectiveDatePolicy.QueryRequired),
        new("master-data.price-list.audit.read", "/api/v1/master-data/price-lists/{priceListId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.price-list.audit", FoundationScopePolicy.Tenant),
        new("master-data.import.create", "/api/v1/master-data/imports", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.import", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("master-data.import.simulate", "/api/v1/master-data/imports/{batchId:guid}/simulate", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.import", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("master-data.import.execute", "/api/v1/master-data/imports/{batchId:guid}/execute", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.import", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("master-data.import.list", "/api/v1/master-data/imports", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.import.view", FoundationScopePolicy.Tenant),
        new("master-data.import.read", "/api/v1/master-data/imports/{batchId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.import.view", FoundationScopePolicy.Tenant),
        new("master-data.import.status.read", "/api/v1/master-data/imports/{batchId:guid}/status", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.import.view", FoundationScopePolicy.Tenant),
        new("master-data.import.rows.read", "/api/v1/master-data/imports/{batchId:guid}/rows", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.import.view", FoundationScopePolicy.Tenant),
        new("master-data.import.reconciliation.read", "/api/v1/master-data/imports/{batchId:guid}/reconciliation", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.import.view", FoundationScopePolicy.Tenant),
        new("master-data.import.audit.read", "/api/v1/master-data/imports/{batchId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.import.audit", FoundationScopePolicy.Tenant),
        new("master-data.import.evidence.read", "/api/v1/master-data/imports/{batchId:guid}/evidence", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.import.view", FoundationScopePolicy.Tenant),
        new("master-data.import.replay", "/api/v1/master-data/imports/{batchId:guid}/rows/{rowId:guid}/replay", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.master-data.import", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.organization-scope.list", "/api/v1/procurement/organization-scopes", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.organization-scope.view", FoundationScopePolicy.Tenant),
        new("procurement.purchase-request.list", "/api/v1/procurement/purchase-requests", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-request.view", FoundationScopePolicy.Tenant),
        new("procurement.purchase-request.read", "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-request.view", FoundationScopePolicy.Tenant),
        new("procurement.purchase-request.create", "/api/v1/procurement/purchase-requests", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-request.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-request.edit", "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-request.edit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-request.submit", "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/submit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-request.submit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-request.approve", "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/approve", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-request.approve", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-request.reject", "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/reject", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-request.reject", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-request.return-for-change", "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/return-for-change", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-request.return", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-request.cancel", "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/cancel", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-request.cancel", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-request.history.read", "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-request.history", FoundationScopePolicy.Tenant),
        new("procurement.purchase-request.audit.read", "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-request.audit", FoundationScopePolicy.Tenant),
        new("procurement.quotation.list", "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/quotations", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.quotation.view", FoundationScopePolicy.Tenant),
        new("procurement.quotation.read", "/api/v1/procurement/quotations/{quotationId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.quotation.view", FoundationScopePolicy.Tenant),
        new("procurement.quotation.create", "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/quotations", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.quotation.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.quotation.edit", "/api/v1/procurement/quotations/{quotationId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.quotation.edit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.quotation.submit", "/api/v1/procurement/quotations/{quotationId:guid}/submit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.quotation.submit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.quotation.withdraw", "/api/v1/procurement/quotations/{quotationId:guid}/withdraw", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.quotation.withdraw", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.quotation.disqualify", "/api/v1/procurement/quotations/{quotationId:guid}/disqualify", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.quotation.disqualify", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.quotation.compare", "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/quotation-comparison", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.quotation.compare", FoundationScopePolicy.Tenant),
        new("procurement.source-decision.read", "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/source-decision", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.source-decision.view", FoundationScopePolicy.Tenant),
        new("procurement.source-decision.history.read", "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/source-decision/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.source-decision.view", FoundationScopePolicy.Tenant),
        new("procurement.source-decision.record", "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/source-decision", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.source-decision.record", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.quotation.history.read", "/api/v1/procurement/quotations/{quotationId:guid}/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.quotation.history", FoundationScopePolicy.Tenant),
        new("procurement.quotation.audit.read", "/api/v1/procurement/quotations/{quotationId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.quotation.audit", FoundationScopePolicy.Tenant),
        new("procurement.purchase-order.source.list", "/api/v1/procurement/purchase-order-sources", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.view", FoundationScopePolicy.Tenant),
        new("procurement.purchase-order.list", "/api/v1/procurement/purchase-orders", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.view", FoundationScopePolicy.Tenant),
        new("procurement.purchase-order.read", "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.view", FoundationScopePolicy.Tenant),
        new("procurement.purchase-order.create", "/api/v1/procurement/purchase-orders", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-order.edit", "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/edit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.edit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-order.submit", "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/submit", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.submit", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-order.approve", "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/approve", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.approve", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-order.reject", "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/reject", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.reject", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-order.return-for-change", "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/return-for-change", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.return", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-order.issue", "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/issue", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.issue", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-order.cancel", "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/cancel", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.cancel", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-order.confirmation.read", "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/confirmations", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.confirmation.view", FoundationScopePolicy.Tenant),
        new("procurement.purchase-order.confirmation.capture", "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/confirmations", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.confirmation.capture", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-order.supplier-change.approve", "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/supplier-change/approve", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.supplier-change.approve", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-order.supplier-change.reject", "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/supplier-change/reject", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.supplier-change.reject", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.purchase-order.history.read", "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.history", FoundationScopePolicy.Tenant),
        new("procurement.purchase-order.audit.read", "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.purchase-order.audit", FoundationScopePolicy.Tenant),
        new("procurement.goods-receipt.eligible-source.list", "/api/v1/procurement/goods-receipt-sources", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.goods-receipt.view", FoundationScopePolicy.Tenant),
        new("procurement.goods-receipt.list", "/api/v1/procurement/goods-receipts", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.goods-receipt.view", FoundationScopePolicy.Tenant),
        new("procurement.goods-receipt.read", "/api/v1/procurement/goods-receipts/{goodsReceiptId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.goods-receipt.view", FoundationScopePolicy.Tenant),
        new("procurement.goods-receipt.create", "/api/v1/procurement/goods-receipts", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.goods-receipt.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.goods-receipt.cancel", "/api/v1/procurement/goods-receipts/{goodsReceiptId:guid}/cancel", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.goods-receipt.cancel", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.goods-receipt.history.read", "/api/v1/procurement/goods-receipts/{goodsReceiptId:guid}/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.goods-receipt.history", FoundationScopePolicy.Tenant),
        new("procurement.goods-receipt.audit.read", "/api/v1/procurement/goods-receipts/{goodsReceiptId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.goods-receipt.audit", FoundationScopePolicy.Tenant),
        new("procurement.invoice-handoff.eligible-source.list", "/api/v1/procurement/purchase-invoice-handoff-sources", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.invoice-handoff.view", FoundationScopePolicy.Tenant),
        new("procurement.invoice-handoff.list", "/api/v1/procurement/purchase-invoice-handoffs", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.invoice-handoff.view", FoundationScopePolicy.Tenant),
        new("procurement.invoice-handoff.read", "/api/v1/procurement/purchase-invoice-handoffs/{purchaseInvoiceHandoffId:guid}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.invoice-handoff.view", FoundationScopePolicy.Tenant),
        new("procurement.invoice-handoff.create", "/api/v1/procurement/purchase-invoice-handoffs", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.invoice-handoff.create", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.invoice-handoff.cancel", "/api/v1/procurement/purchase-invoice-handoffs/{purchaseInvoiceHandoffId:guid}/cancel", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.invoice-handoff.cancel", FoundationScopePolicy.Tenant, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true, Concurrency: FoundationConcurrencyPolicy.IfMatch, Idempotency: FoundationIdempotencyPolicy.Required),
        new("procurement.invoice-handoff.history.read", "/api/v1/procurement/purchase-invoice-handoffs/{purchaseInvoiceHandoffId:guid}/history", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.invoice-handoff.history", FoundationScopePolicy.Tenant),
        new("procurement.invoice-handoff.audit.read", "/api/v1/procurement/purchase-invoice-handoffs/{purchaseInvoiceHandoffId:guid}/audit", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, "tenant.procurement.invoice-handoff.audit", FoundationScopePolicy.Tenant),
        new("auth.antiforgery.read", "/api/v1/auth/antiforgery", "GET", FoundationSecurityProfile.Anonymous, FoundationOperationVisibility.Public),
        new("auth.sign-in", "/api/v1/auth/sign-in", "POST", FoundationSecurityProfile.Anonymous, FoundationOperationVisibility.Public, RequiresAntiforgery: false, IsUnsafe: true),
        new("auth.development-bypass", "/api/v1/auth/development-bypass", "POST", FoundationSecurityProfile.Anonymous, FoundationOperationVisibility.Public, RequiresAntiforgery: false, IsUnsafe: true),
        new("auth.sign-out", "/api/v1/auth/sign-out", "POST", FoundationSecurityProfile.AuthenticatedSession, FoundationOperationVisibility.Public, "authenticated.session", FoundationScopePolicy.None, RequiresAntiforgery: true, RequiresMandatoryAudit: false, IsUnsafe: true),
        new("auth.session.read", "/api/v1/auth/session", "GET", FoundationSecurityProfile.AuthenticatedSession, FoundationOperationVisibility.Public, "authenticated.session"),
        new("auth.contexts.read", "/api/v1/auth/contexts", "GET", FoundationSecurityProfile.AuthenticatedSession, FoundationOperationVisibility.Public, "authenticated.session"),
        new("auth.entry.read", "/api/v1/auth/entry", "GET", FoundationSecurityProfile.AuthenticatedSession, FoundationOperationVisibility.Public, "authenticated.session"),
        new("auth.operational-contexts.read", "/api/v1/auth/operational-contexts", "GET", FoundationSecurityProfile.AuthenticatedSession, FoundationOperationVisibility.Public, "authenticated.session"),
        new("auth.context-switch", "/api/v1/auth/context-switch", "POST", FoundationSecurityProfile.AuthenticatedSession, FoundationOperationVisibility.Public, "foundation.context.switch", FoundationScopePolicy.None, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("auth.operational-context-switch", "/api/v1/auth/operational-context-switch", "POST", FoundationSecurityProfile.AuthenticatedSession, FoundationOperationVisibility.Public, "foundation.context.switch", FoundationScopePolicy.None, RequiresAntiforgery: true, RequiresMandatoryAudit: true, IsUnsafe: true)
    ];

    /// <summary>Internal operations deliberately excluded from public routing.</summary>
    public static IReadOnlyList<FoundationOperationDescriptor> InternalOperations { get; } =
    [
        new("identity.invitation.issue", "", "INTERNAL", FoundationSecurityProfile.HighRisk, FoundationOperationVisibility.Internal),
        new("identity.recovery.consume", "", "INTERNAL", FoundationSecurityProfile.HighRisk, FoundationOperationVisibility.Internal)
    ];

    /// <summary>Gets one public or internal descriptor or throws when unknown.</summary>
    public static FoundationOperationDescriptor GetRequired(string operationId) =>
        PublicOperations.Concat(InternalOperations).SingleOrDefault(item => string.Equals(item.OperationId, operationId, StringComparison.Ordinal))
        ?? throw new KeyNotFoundException($"Unknown Foundation operation: {operationId}");

    /// <summary>Resolves one public or internal descriptor without throwing.</summary>
    public static bool TryGet(string? operationId, out FoundationOperationDescriptor descriptor)
    {
        descriptor = null!;
        if (string.IsNullOrWhiteSpace(operationId))
        {
            return false;
        }

        var found = PublicOperations.Concat(InternalOperations)
            .SingleOrDefault(item => string.Equals(item.OperationId, operationId.Trim(), StringComparison.Ordinal));
        if (found is null)
        {
            return false;
        }

        descriptor = found;
        return true;
    }
}
