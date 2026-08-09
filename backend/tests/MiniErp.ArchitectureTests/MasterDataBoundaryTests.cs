using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.BusinessParties;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.MasterData;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class MasterDataBoundaryTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid CompanyA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CompanyB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ActorA = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SessionA = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void Operation_catalog_covers_every_defined_operation()
    {
        foreach (var operation in Enum.GetValues<MasterDataOperation>())
        {
            Assert.True(
                MasterDataOperationCatalog.TryGetRequiredCapability(operation, out _),
                $"No server-owned capability mapping exists for {operation}.");
        }
    }

    [Fact]
    public void Operation_catalog_maps_each_operation_to_its_exact_capability()
    {
        var expected = new Dictionary<MasterDataOperation, MasterDataCapability>
        {
            [MasterDataOperation.View] = MasterDataCapability.View,
            [MasterDataOperation.Create] = MasterDataCapability.Create,
            [MasterDataOperation.Edit] = MasterDataCapability.Edit,
            [MasterDataOperation.Activate] = MasterDataCapability.Activate,
            [MasterDataOperation.Deactivate] = MasterDataCapability.Deactivate,
            [MasterDataOperation.Approve] = MasterDataCapability.Approve,
            [MasterDataOperation.Import] = MasterDataCapability.ImportMigrate,
            [MasterDataOperation.ViewAuditHistory] = MasterDataCapability.ViewAuditHistory,
            [MasterDataOperation.Reactivate] = MasterDataCapability.Activate
        };

        foreach (var (operation, capability) in expected)
        {
            Assert.True(MasterDataOperationCatalog.TryGetRequiredCapability(operation, out var actual));
            Assert.Equal(capability, actual);
        }
    }

    [Fact]
    public void Unknown_operation_fails_closed_before_policy_evaluation()
    {
        var policy = new CountingAllowingResourcePolicy();
        var service = new MasterDataResourceAuthorizationService(
            new GrantingCapabilityResolver(MasterDataCapability.View),
            policy,
            new DefaultMasterDataApprovalPolicy(),
            new ExactTrustedScopePolicy());

        var result = service.Authorize(
            ResolveContext(),
            Resource(TenantA, CompanyA, "product-001"),
            (MasterDataOperation)999);

        Assert.False(result.Allowed);
        Assert.Equal("authorization_operation_unmapped", result.Code);
        Assert.Equal(0, policy.EvaluationCount);
    }

    [Fact]
    public void Caller_cannot_pair_an_operation_with_a_weaker_unrelated_capability()
    {
        var service = AllowingAuthorizationService(MasterDataCapability.View);

        var result = service.Authorize(
            ResolveContext(),
            Resource(TenantA, CompanyA, "product-001"),
            MasterDataOperation.Create);

        Assert.False(result.Allowed);
        Assert.Equal("permission_denied", result.Code);
    }

    [Fact]
    public void Master_data_and_business_parties_are_explicit_composition_seams()
    {
        var masterData = MasterDataModuleRegistration.Create();
        var businessParties = BusinessPartiesModuleRegistration.Create();

        Assert.Equal("master-data-catalog", masterData.Descriptor.Key);
        Assert.Equal("Master Data and Catalog", masterData.Descriptor.Name);
        Assert.Equal("MasterDataAndCatalog", masterData.Descriptor.Boundary);
        Assert.True(masterData.RegistrationEvidence.IsRegistered);
        Assert.False(masterData.GetType().IsPublic);

        Assert.Equal("business-parties", businessParties.Descriptor.Key);
        Assert.Equal("Business Parties", businessParties.Descriptor.Name);
        Assert.Equal("BusinessParties", businessParties.Descriptor.Boundary);
        Assert.True(businessParties.RegistrationEvidence.IsRegistered);
        Assert.False(businessParties.GetType().IsPublic);
    }

    [Fact]
    public void Stable_vocabulary_excludes_unresolved_product_item_and_lifecycle_decisions()
    {
        Assert.DoesNotContain("Item", Enum.GetNames<MasterDataResourceKind>(), StringComparer.Ordinal);
        Assert.DoesNotContain("Sku", Enum.GetNames<MasterDataResourceKind>(), StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("Barcode", Enum.GetNames<MasterDataResourceKind>(), StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("Draft", Enum.GetNames<MasterDataOperation>(), StringComparer.Ordinal);
        Assert.DoesNotContain("Active", Enum.GetNames<MasterDataOperation>(), StringComparer.Ordinal);

        var tenant = new TenantOwnership(TenantA);
        var scope = new BusinessScope(tenant, organizationAnchor: null, new ScopePolicyReference("pending.policy", 1));

        Assert.Null(scope.OrganizationAnchor);
        Assert.DoesNotContain(
            typeof(BusinessScope).GetProperties(),
            property => property.Name.Contains("TenantWide", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void M95_SL_01_production_surface_has_no_persistence_or_unresolved_business_behavior()
    {
        var appSourcePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "MiniErp.App",
            "Modules",
            "MasterData"));
        var contractSourcePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "MiniErp.Contracts",
            "Modules",
            "MasterData"));
        var sourceFiles = Directory.GetFiles(appSourcePath, "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(contractSourcePath, "*.cs", SearchOption.AllDirectories))
            .ToArray();
        var forbiddenIdentifiers = new[]
        {
            "DbContext",
            "EntityFrameworkCore",
            "Migration",
            "SqlConnection",
            "CreateTable",
            "Item",
            "SKU",
            "Barcode",
            "Batch",
            "Lot",
            "Serial",
            "Expiry",
            "Draft",
            "TenantWide"
        };

        Assert.NotEmpty(sourceFiles);
        foreach (var sourceFile in sourceFiles)
        {
            var source = File.ReadAllText(sourceFile);
            var tree = CSharpSyntaxTree.ParseText(source, path: sourceFile);
            var identifiers = tree.GetRoot()
                .DescendantTokens()
                .Where(token => token.RawKind == (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.IdentifierToken)
                .Select(token => token.ValueText)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var forbiddenToken in forbiddenIdentifiers)
            {
                Assert.DoesNotContain(forbiddenToken, identifiers);
            }
        }
    }

    [Fact]
    public void Organization_anchors_cannot_cross_tenants_and_same_codes_remain_tenant_local()
    {
        var tenantA = new TenantOwnership(TenantA);
        var tenantB = new TenantOwnership(TenantB);
        var policy = new ScopePolicyReference("organization.exact", 1);

        Assert.Throws<ArgumentException>(() => new BusinessScope(
            default,
            organizationAnchor: null,
            policy));
        Assert.Throws<ArgumentException>(() => new MasterDataResourceReference(
            MasterDataResourceKind.Product,
            default,
            stableId: Guid.NewGuid(),
            businessCode: "invalid"));
        Assert.Throws<ArgumentException>(() => new BusinessScope(
            tenantA,
            new OrganizationReference(tenantB, OrganizationScopeKind.Company, CompanyA),
            policy));

        var resourceA = Resource(TenantA, CompanyA, "same-code");
        var resourceB = Resource(TenantB, CompanyA, "same-code");

        Assert.Equal(resourceA.BusinessCode, resourceB.BusinessCode);
        Assert.NotEqual(resourceA.Tenant, resourceB.Tenant);
    }

    [Fact]
    public void Resolver_accepts_one_server_derived_ordinary_membership_context()
    {
        var result = new MasterDataTenantContextResolver().Resolve(CreateFoundationContext());

        Assert.True(result.Allowed);
        var context = Assert.IsType<MasterDataRequestContext>(result.Context);
        Assert.Equal(TenantA, context.TenantId.Value);
        Assert.Equal(TenantAuthorizationPath.OrdinaryMembership, context.AuthorizationPath);
        Assert.Equal(ActorA, context.ActorId);
        Assert.Equal(SessionA, context.SessionId);
        Assert.Equal($"Company:{CompanyA:D}", context.TrustedScope!.Value.Value);
        Assert.Equal("corr-master-data", context.CorrelationId!.Value.Value);
    }

    [Fact]
    public void Resolver_accepts_empty_selection_without_changing_trusted_scope()
    {
        var result = new MasterDataTenantContextResolver().Resolve(
            CreateFoundationContext(),
            new MasterDataScopeSelection());

        Assert.True(result.Allowed);
        var context = Assert.IsType<MasterDataRequestContext>(result.Context);
        Assert.Equal(TenantA, context.TenantId.Value);
        Assert.Equal($"Company:{CompanyA:D}", context.TrustedScope!.Value.Value);
    }

    [Fact]
    public void Resolver_accepts_same_tenant_only_hint_and_preserves_server_scope()
    {
        var result = new MasterDataTenantContextResolver().Resolve(
            CreateFoundationContext(),
            new MasterDataScopeSelection(requestedTenantId: TenantA));

        Assert.True(result.Allowed);
        var context = Assert.IsType<MasterDataRequestContext>(result.Context);
        Assert.Equal(TenantA, context.TenantId.Value);
        Assert.Equal($"Company:{CompanyA:D}", context.TrustedScope!.Value.Value);
    }

    [Fact]
    public void Resolver_preserves_support_grant_as_a_distinct_tenant_path()
    {
        var result = new MasterDataTenantContextResolver().Resolve(CreateFoundationContext(supportGrant: true));

        Assert.True(result.Allowed);
        Assert.Equal(
            TenantAuthorizationPath.SupportGrant,
            Assert.IsType<MasterDataRequestContext>(result.Context).AuthorizationPath);
    }

    [Fact]
    public void Resolver_rejects_anonymous_authenticated_session_and_platform_contexts()
    {
        var resolver = new MasterDataTenantContextResolver();

        Assert.False(resolver.Resolve(FoundationRequestContext.Unauthenticated()).Allowed);
        Assert.False(resolver.Resolve(
            FoundationRequestContext.ForAuthenticatedSession(ActorA, SessionA, "master-data.view")).Allowed);

        var platform = FoundationRequestContext.ForPlatform(
            ActorA,
            SessionA,
            new PlatformGovernanceContext(
                ActorA,
                PlatformGovernancePurpose.SecurityEvidence,
                new CorrelationId("corr-platform")),
            "master-data.view");

        var platformResult = resolver.Resolve(platform);
        Assert.False(platformResult.Allowed);
        Assert.Equal("tenant_context_failed", platformResult.Code);
    }

    [Fact]
    public void Client_tenant_and_scope_selection_cannot_replace_server_authority()
    {
        var resolver = new MasterDataTenantContextResolver();
        var sameTenant = resolver.Resolve(
            CreateFoundationContext(),
            new MasterDataScopeSelection(
                TenantA,
                Scope(TenantA, CompanyA)));
        var foreignTenant = resolver.Resolve(
            CreateFoundationContext(),
            new MasterDataScopeSelection(TenantB, Scope(TenantB, CompanyA)));
        var siblingScope = resolver.Resolve(
            CreateFoundationContext(),
            new MasterDataScopeSelection(TenantA, Scope(TenantA, CompanyB)));

        Assert.True(sameTenant.Allowed);
        var resolvedSameTenant = Assert.IsType<MasterDataRequestContext>(sameTenant.Context);
        Assert.Equal(TenantA, resolvedSameTenant.TenantId.Value);
        Assert.Equal($"Company:{CompanyA:D}", resolvedSameTenant.TrustedScope!.Value.Value);
        Assert.False(foreignTenant.Allowed);
        Assert.Equal("cross_tenant_target_denied", foreignTenant.Code);
        Assert.Null(foreignTenant.Context);
        Assert.False(siblingScope.Allowed);
        Assert.Equal("resource_scope_denied", siblingScope.Code);
    }

    [Fact]
    public void Resource_authorization_denies_foreign_tenant_and_scope_targets_without_disclosure()
    {
        var context = ResolveContext();
        var service = AllowingAuthorizationService(MasterDataCapability.View);

        var foreignTenant = service.Authorize(
            context,
            Resource(TenantB, CompanyA, "same-code"),
            MasterDataOperation.View);
        var siblingScope = service.Authorize(
            context,
            Resource(TenantA, CompanyB, "same-code"),
            MasterDataOperation.View);
        var noScope = service.Authorize(
            context,
            new MasterDataResourceReference(
                MasterDataResourceKind.Product,
                new TenantOwnership(TenantA),
                stableId: Guid.NewGuid(),
                businessCode: "same-code"),
            MasterDataOperation.View);

        Assert.False(foreignTenant.Allowed);
        Assert.Equal("cross_tenant_target_denied", foreignTenant.Code);
        Assert.False(siblingScope.Allowed);
        Assert.Equal("resource_scope_denied", siblingScope.Code);
        Assert.False(noScope.Allowed);
        Assert.Equal("resource_scope_denied", noScope.Code);
    }

    [Fact]
    public void Server_capability_resolution_is_required_before_policy_evaluation()
    {
        var context = ResolveContext();
        var service = AllowingAuthorizationService(MasterDataCapability.View);

        var view = service.Authorize(
            context,
            Resource(TenantA, CompanyA, "product-001"),
            MasterDataOperation.View);
        var create = service.Authorize(
            context,
            Resource(TenantA, CompanyA, "product-001"),
            MasterDataOperation.Create);

        Assert.True(view.Allowed);
        Assert.False(create.Allowed);
        Assert.Equal("permission_denied", create.Code);
    }

    [Fact]
    public void Mutations_fail_closed_when_approval_policy_is_not_configured()
    {
        var context = ResolveContext();
        var service = new MasterDataResourceAuthorizationService(
            new GrantingCapabilityResolver(MasterDataCapability.Create),
            new AllowingResourcePolicy(),
            new DefaultMasterDataApprovalPolicy(),
            new ExactTrustedScopePolicy());

        var result = service.Authorize(
            context,
            Resource(TenantA, CompanyA, "product-001"),
            MasterDataOperation.Create);

        Assert.False(result.Allowed);
        Assert.Equal("approval_policy_not_configured", result.Code);
    }

    [Fact]
    public void Approved_mutations_cannot_be_self_approved()
    {
        var context = ResolveContext();
        var service = new MasterDataResourceAuthorizationService(
            new GrantingCapabilityResolver(MasterDataCapability.Edit),
            new AllowingResourcePolicy(),
            new FixedApprovalPolicy(new MasterDataApprovalPolicyResult(
                MasterDataApprovalPolicyStatus.Approved,
                Guid.NewGuid(),
                context.ActorId)),
            new ExactTrustedScopePolicy());

        var result = service.Authorize(
            context,
            Resource(TenantA, CompanyA, "product-001"),
            MasterDataOperation.Edit);

        Assert.False(result.Allowed);
        Assert.Equal("self_approval_denied", result.Code);
    }

    [Fact]
    public async Task Audit_evidence_uses_trusted_context_and_bridges_to_foundation()
    {
        var context = ResolveContext();
        var resource = Resource(TenantA, CompanyA, "product-001");
        var decision = MasterDataPolicyDecision.Allowed();
        var evidence = MasterDataAuditEvidenceFactory.Create(
            context,
            resource,
            MasterDataOperation.View,
            decision,
            FoundationAuditReason.Allowed,
            beforeSummary: "before",
            afterSummary: "after");

        Assert.Equal(TenantA, evidence.Tenant.TenantId);
        Assert.Equal(ActorA, evidence.ActorId);
        Assert.Equal(SessionA, evidence.SessionId);
        Assert.Equal("corr-master-data", evidence.CorrelationId);
        Assert.Equal(FoundationAuditAuthorizationPath.OrdinaryMembership, evidence.AuthorizationPath);
        Assert.Equal(MasterDataResourceKind.Product, evidence.ResourceKind);
        Assert.Equal("product-001", evidence.BusinessCode);
        Assert.Equal("before", evidence.BeforeSummary);
        Assert.Equal("after", evidence.AfterSummary);

        var foundationStore = new MiniErp.App.Modules.Audit.LocalImmutableAuditEvidenceStore();
        var sink = new FoundationMasterDataAuditEvidenceSink(context, foundationStore);
        var result = await new MasterDataAuditCoordinator(sink).RecordAsync(
            context,
            resource,
            MasterDataOperation.View,
            decision,
            FoundationAuditReason.Allowed,
            cancellationToken: CancellationToken.None);

        Assert.True(result.Appended);
        var foundationEntries = await foundationStore.ReadForTenantAsync(context.FoundationContext.TenantContext!);
        Assert.Single(foundationEntries);
        Assert.Equal(evidence.AuthorizationPath, foundationEntries[0].AuthorizationPath);
        Assert.Equal(evidence.Tenant.TenantId, foundationEntries[0].TenantId);
        Assert.Equal(evidence.CorrelationId, foundationEntries[0].CorrelationId);
    }

    [Fact]
    public void Audit_factory_rejects_a_foreign_tenant_target()
    {
        var context = ResolveContext();

        Assert.Throws<ArgumentException>(() => MasterDataAuditEvidenceFactory.Create(
            context,
            Resource(TenantB, CompanyA, "foreign"),
            MasterDataOperation.View,
            MasterDataPolicyDecision.Denied("cross_tenant_target_denied"),
            FoundationAuditReason.CrossTenantTargetDenied));
    }

    private static MasterDataResourceAuthorizationService AllowingAuthorizationService(
        params MasterDataCapability[] capabilities) => new(
        new GrantingCapabilityResolver(capabilities),
        new AllowingResourcePolicy(),
        new DefaultMasterDataApprovalPolicy(),
        new ExactTrustedScopePolicy());

    private static MasterDataRequestContext ResolveContext()
    {
        var result = new MasterDataTenantContextResolver().Resolve(CreateFoundationContext());
        return Assert.IsType<MasterDataRequestContext>(result.Context);
    }

    private static FoundationRequestContext CreateFoundationContext(
        bool supportGrant = false,
        Guid? tenantId = null,
        Guid? actorId = null,
        Guid? sessionId = null)
    {
        var actor = actorId ?? ActorA;
        var tenant = tenantId ?? TenantA;
        var tenantContext = supportGrant
            ? TenantContext.ForSupportGrant(
                new TenantId(tenant),
                new SupportGrantReference(
                    Guid.Parse("55555555-5555-5555-5555-555555555555"),
                    Guid.Parse("66666666-6666-6666-6666-666666666666")),
                new ScopeReference($"Company:{CompanyA:D}"),
                new CorrelationId("corr-master-data"),
                actor)
            : TenantContext.ForOrdinaryMembership(
                new TenantId(tenant),
                new MembershipReference(Guid.Parse("77777777-7777-7777-7777-777777777777")),
                new ScopeReference($"Company:{CompanyA:D}"),
                new CorrelationId("corr-master-data"),
                actor);

        return FoundationRequestContext.ForTenant(
            actor,
            sessionId ?? SessionA,
            tenantContext,
            "master-data.view");
    }

    private static BusinessScope Scope(Guid tenantId, Guid companyId) => new(
        new TenantOwnership(tenantId),
        new OrganizationReference(new TenantOwnership(tenantId), OrganizationScopeKind.Company, companyId),
        new ScopePolicyReference("organization.exact", 1));

    private static MasterDataResourceReference Resource(Guid tenantId, Guid companyId, string code) =>
        new(
            MasterDataResourceKind.Product,
            new TenantOwnership(tenantId),
            Guid.NewGuid(),
            code,
            Scope(tenantId, companyId));

    private sealed class GrantingCapabilityResolver : IMasterDataCapabilityResolver
    {
        private readonly IReadOnlySet<MasterDataCapability> capabilities;

        public GrantingCapabilityResolver(params MasterDataCapability[] capabilities)
        {
            this.capabilities = capabilities.ToHashSet();
        }

        public IReadOnlySet<MasterDataCapability> Resolve(MasterDataRequestContext context)
        {
            Assert.NotNull(context);
            return capabilities;
        }
    }

    private sealed class AllowingResourcePolicy : IMasterDataResourcePolicy
    {
        public MasterDataPolicyDecision Evaluate(MasterDataPolicyInput input)
        {
            Assert.NotNull(input);
            return MasterDataPolicyDecision.Allowed();
        }
    }

    private sealed class CountingAllowingResourcePolicy : IMasterDataResourcePolicy
    {
        public int EvaluationCount { get; private set; }

        public MasterDataPolicyDecision Evaluate(MasterDataPolicyInput input)
        {
            EvaluationCount++;
            return MasterDataPolicyDecision.Allowed();
        }
    }

    private sealed class ExactTrustedScopePolicy : IMasterDataScopePolicy
    {
        public MasterDataScopeDecision Evaluate(
            MasterDataRequestContext context,
            MasterDataResourceReference resource)
        {
            Assert.NotNull(context);
            if (resource.Scope?.OrganizationAnchor is not { } anchor
                || context.TrustedScope is not { } trustedScope
                || !string.Equals(anchor.CanonicalValue, trustedScope.Value, StringComparison.Ordinal))
            {
                return MasterDataScopeDecision.Denied("resource_scope_denied");
            }

            return MasterDataScopeDecision.Success();
        }
    }

    private sealed class FixedApprovalPolicy : IMasterDataApprovalPolicy
    {
        private readonly MasterDataApprovalPolicyResult result;

        public FixedApprovalPolicy(MasterDataApprovalPolicyResult result)
        {
            this.result = result;
        }

        public MasterDataApprovalPolicyResult Evaluate(
            MasterDataRequestContext context,
            MasterDataResourceReference resource,
            MasterDataOperation operation)
        {
            Assert.NotNull(context);
            Assert.NotNull(resource);
            return result;
        }
    }
}
