using System.Reflection;
using System.Text.Json;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Audit;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Foundation;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class AuditObservabilityTests
{
    [Fact]
    public async Task Ordinary_tenant_success_records_exact_path_and_trusted_tenant()
    {
        var store = new LocalImmutableAuditEvidenceStore();
        var result = await CreateCoordinator(store).RecordAsync(
            OrdinaryContext(),
            "foundation.probe.write",
            "corr-mesp60-001",
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed,
            idempotencyKey: "idem-001",
            operationVersion: "7");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Evidence);
        Assert.Equal(FoundationAuditAuthorizationPath.OrdinaryMembership, result.Evidence!.AuthorizationPath);
        Assert.Equal(TenantA.Value, result.Evidence.TenantId);
        Assert.Equal(ActorA, result.Evidence.ActorId);
        Assert.Equal("7", result.Evidence.OperationVersion);
    }

    [Fact]
    public async Task Support_success_records_support_grant_path_and_safe_facts()
    {
        var result = await CreateCoordinator(new LocalImmutableAuditEvidenceStore()).RecordAsync(
            SupportContext(),
            "foundation.support-context.read",
            "corr-support-001",
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed,
            supportPurpose: "case investigation",
            supportGrantExpiresAt: new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.True(result.Succeeded);
        var evidence = Assert.IsType<FoundationAuditEvidence>(result.Evidence);
        Assert.Equal(FoundationAuditAuthorizationPath.SupportGrant, evidence.AuthorizationPath);
        Assert.Equal(ActorA, evidence.SupportUserId);
        Assert.NotNull(evidence.SupportGrantId);
        Assert.NotNull(evidence.SupportCaseId);
        Assert.Equal("case investigation", evidence.Purpose);
        Assert.Equal(TenantA.Value, evidence.TenantId);
        Assert.NotNull(evidence.SupportGrantExpiresAt);
    }

    [Fact]
    public async Task Platform_governance_records_platform_path_without_tenant()
    {
        var result = await CreateCoordinator(new LocalImmutableAuditEvidenceStore()).RecordAsync(
            PlatformContext(),
            "foundation.audit.read",
            "corr-platform-001",
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed);

        Assert.True(result.Succeeded);
        var evidence = Assert.IsType<FoundationAuditEvidence>(result.Evidence);
        Assert.Equal(FoundationAuditAuthorizationPath.PlatformGovernanceContext, evidence.AuthorizationPath);
        Assert.Null(evidence.TenantId);
        Assert.Equal(nameof(PlatformGovernancePurpose.SecurityEvidence), evidence.Purpose);
    }

    [Fact]
    public void Ordinary_and_support_paths_cannot_be_combined()
    {
        Assert.Throws<ArgumentException>(() => FoundationAuditEvidenceFactory.Create(
            OrdinaryContext(),
            "foundation.probe.write",
            "corr-path-001",
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed,
            supportPurpose: "not allowed"));
    }

    [Fact]
    public async Task Platform_governance_cannot_query_tenant_business_evidence()
    {
        var store = new LocalImmutableAuditEvidenceStore();
        await store.AppendAsync(FoundationAuditEvidenceFactory.Create(
            OrdinaryContext(),
            "foundation.target.read",
            "corr-tenant-001",
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed));

        var platformEvidence = await store.ReadForPlatformAsync(PlatformGovernanceContextValue());

        Assert.Empty(platformEvidence);
    }

    [Fact]
    public async Task Denied_cross_tenant_attempt_records_safe_reason_without_foreign_target()
    {
        var store = new LocalImmutableAuditEvidenceStore();
        var result = await CreateCoordinator(store).RecordAsync(
            OrdinaryContext(),
            "foundation.target.read",
            "corr-deny-001",
            FoundationAuditDecision.Denied,
            FoundationAuditReason.CrossTenantTargetDenied);

        var evidence = Assert.IsType<FoundationAuditEvidence>(result.Evidence);
        var serialized = JsonSerializer.Serialize(evidence);
        Assert.Equal(FoundationAuditReason.CrossTenantTargetDenied, evidence.Reason);
        Assert.DoesNotContain(TenantB.Value.ToString("D"), serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TargetId", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("password", "password-value")]
    [InlineData("token", "token-value")]
    [InlineData("cookie", "cookie-value")]
    [InlineData("mfa", "opaque-mfa-value")]
    [InlineData("providerError", "SqlException: server details")]
    [InlineData("requestPayload", "arbitrary business payload")]
    public void Sensitive_inputs_are_ignored_by_the_allow_list(string key, string value)
    {
        var evidence = FoundationAuditEvidenceFactory.Create(
            OrdinaryContext(),
            "foundation.probe.write",
            "corr-redact-001",
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed);
        var telemetry = FoundationAuditRedactor.ToTelemetry(
            evidence,
            FoundationAuditTelemetryKind.StructuredLog,
            new Dictionary<string, string?> { [key] = value });
        var serialized = JsonSerializer.Serialize(telemetry);

        Assert.DoesNotContain(value, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(key, serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Newline_and_control_injection_is_rejected_before_evidence_creation()
    {
        Assert.Throws<ArgumentException>(() => FoundationAuditEvidenceFactory.Create(
            OrdinaryContext(),
            "foundation.probe.write\r\nforged=true",
            "corr-injection-001",
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed));
        Assert.Throws<ArgumentException>(() => FoundationAuditEvidenceFactory.Create(
            OrdinaryContext(),
            "foundation.probe.write",
            "corr-injection-001\nforged=true",
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed));
    }

    [Fact]
    public async Task Evidence_is_append_only()
    {
        var store = new LocalImmutableAuditEvidenceStore();
        var evidence = FoundationAuditEvidenceFactory.Create(
            OrdinaryContext(),
            "foundation.probe.write",
            "corr-append-001",
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed);

        await store.AppendAsync(evidence);

        Assert.Single(await store.ReadForTenantAsync(TenantContextA()));
        Assert.DoesNotContain(
            typeof(LocalImmutableAuditEvidenceStore)
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
            method => method.Name is "Update" or "Delete" or "Remove" or "Purge" or "Clear");
    }

    [Fact]
    public async Task Evidence_id_reuse_is_denied_without_replacement()
    {
        var store = new LocalImmutableAuditEvidenceStore();
        var evidence = FoundationAuditEvidenceFactory.Create(
            OrdinaryContext(),
            "foundation.probe.write",
            "corr-append-002",
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed);

        await store.AppendAsync(evidence);
        await Assert.ThrowsAsync<FoundationAuditAppendException>(async () => await store.AppendAsync(evidence));
        Assert.Single(await store.ReadForTenantAsync(TenantContextA()));
    }

    [Fact]
    public void Evidence_contract_has_no_writable_public_properties()
    {
        var writable = typeof(FoundationAuditEvidence)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanWrite)
            .ToArray();

        Assert.Empty(writable);
    }

    [Fact]
    public void Public_audit_surface_has_no_update_delete_or_unscoped_query()
    {
        var publicMethods = typeof(LocalImmutableAuditEvidenceStore)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("Update", publicMethods);
        Assert.DoesNotContain("Delete", publicMethods);
        Assert.DoesNotContain("Query", publicMethods);
        Assert.Contains(nameof(LocalImmutableAuditEvidenceStore.ReadForTenantAsync), publicMethods);
        Assert.Contains(nameof(LocalImmutableAuditEvidenceStore.ReadForPlatformAsync), publicMethods);
    }

    [Fact]
    public async Task Tenant_a_cannot_read_tenant_b_evidence()
    {
        var store = new LocalImmutableAuditEvidenceStore();
        await store.AppendAsync(FoundationAuditEvidenceFactory.Create(
            OrdinaryContext(TenantA, ActorA),
            "foundation.probe.write",
            "corr-a-001",
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed));
        await store.AppendAsync(FoundationAuditEvidenceFactory.Create(
            OrdinaryContext(TenantB, ActorB),
            "foundation.probe.write",
            "corr-b-001",
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed));

        var tenantA = await store.ReadForTenantAsync(TenantContextA());
        var tenantB = await store.ReadForTenantAsync(TenantContextB());

        Assert.Single(tenantA);
        Assert.Single(tenantB);
        Assert.Equal(TenantA.Value, tenantA[0].TenantId);
        Assert.Equal(TenantB.Value, tenantB[0].TenantId);
    }

    [Fact]
    public async Task Repeated_idempotent_operation_remains_attributable()
    {
        var coordinator = CreateCoordinator(new LocalImmutableAuditEvidenceStore());
        var first = await coordinator.RecordAsync(
            OrdinaryContext(),
            "foundation.probe.write",
            "corr-retry-001",
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed,
            idempotencyKey: "idem-retry");
        var retry = await coordinator.RecordRetryAsync(
            OrdinaryContext(),
            "foundation.probe.write",
            "corr-retry-001",
            first.Evidence!.EvidenceId,
            attempt: 2,
            idempotencyKey: "idem-retry");

        Assert.True(retry.Succeeded);
        Assert.Equal(FoundationAuditDecision.Retry, retry.Evidence!.Decision);
        Assert.Equal(first.Evidence.EvidenceId, retry.Evidence.RetryOfEvidenceId);
    }

    [Fact]
    public async Task Retry_does_not_overwrite_prior_evidence()
    {
        var store = new LocalImmutableAuditEvidenceStore();
        var coordinator = CreateCoordinator(store);
        var first = await coordinator.RecordAsync(
            OrdinaryContext(),
            "foundation.probe.write",
            "corr-retry-002",
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed,
            idempotencyKey: "idem-retry-2");
        _ = await coordinator.RecordRetryAsync(
            OrdinaryContext(),
            "foundation.probe.write",
            "corr-retry-002",
            first.Evidence!.EvidenceId,
            attempt: 2,
            idempotencyKey: "idem-retry-2");

        var records = await store.ReadForTenantAsync(TenantContextA());
        Assert.Equal(2, records.Count);
        Assert.Equal(FoundationAuditDecision.Allowed, records[0].Decision);
        Assert.Equal(FoundationAuditDecision.Retry, records[1].Decision);
    }

    [Fact]
    public async Task Mandatory_evidence_failure_fails_closed_before_protected_effect()
    {
        var signals = new LocalFoundationAuditOperationalSignalSink();
        var coordinator = new FoundationAuditCoordinator(
            new FailingEvidenceSink(),
            new LocalFoundationAuditTelemetrySink(),
            signals);
        var effectCalled = false;

        var result = await coordinator.ExecuteProtectedAsync(
            OrdinaryContext(),
            "foundation.probe.write",
            "corr-fail-001",
            FoundationAuditReason.Allowed,
            () =>
            {
                effectCalled = true;
                return Task.FromResult("committed");
            });

        Assert.False(result.Succeeded);
        Assert.Equal("audit_evidence_unavailable", result.Code);
        Assert.False(effectCalled);
        Assert.Single(signals.Signals);
    }

    [Fact]
    public async Task Evidence_failure_signal_is_safe_and_bounded()
    {
        var signals = new LocalFoundationAuditOperationalSignalSink(capacity: 1);
        var coordinator = new FoundationAuditCoordinator(
            new FailingEvidenceSink(),
            new LocalFoundationAuditTelemetrySink(),
            signals);

        _ = await coordinator.RecordAsync(
            OrdinaryContext(),
            "foundation.probe.write",
            "corr-fail-002",
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed);
        _ = await coordinator.RecordAsync(
            OrdinaryContext(),
            "foundation.probe.write",
            "corr-fail-002",
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed);

        Assert.Single(signals.Signals);
        Assert.Equal("audit_evidence_failure", signals.Signals[0].Code);
        Assert.DoesNotContain("password", JsonSerializer.Serialize(signals.Signals), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Correlation_propagates_from_rest_foundation_context()
    {
        var context = FoundationRequestContext.ForTenant(
            ActorA,
            SessionA,
            TenantContext.ForOrdinaryMembership(
                TenantA,
                new MembershipReference(Guid.NewGuid()),
                correlationId: new CorrelationId("corr-from-mesp60"),
                actorId: ActorA),
            "foundation.probe.write");
        var result = await CreateCoordinator(new LocalImmutableAuditEvidenceStore()).RecordAsync(
            context,
            "foundation.probe.write",
            "corr-from-mesp60",
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed);

        Assert.Equal("corr-from-mesp60", result.Evidence!.CorrelationId);
    }

    [Fact]
    public async Task Operation_identifier_propagates_from_rest_foundation_contract()
    {
        var result = await CreateCoordinator(new LocalImmutableAuditEvidenceStore()).RecordAsync(
            OrdinaryContext(),
            "foundation.target.read",
            "corr-op-001",
            FoundationAuditDecision.Denied,
            FoundationAuditReason.NotFound);

        Assert.Equal("foundation.target.read", result.Evidence!.OperationId);
    }

    [Fact]
    public async Task OpenTelemetry_hook_emits_only_safe_allow_listed_fields()
    {
        var telemetry = new LocalFoundationAuditTelemetrySink();
        var result = await new FoundationAuditCoordinator(
            new LocalImmutableAuditEvidenceStore(),
            telemetry,
            new LocalFoundationAuditOperationalSignalSink()).RecordAsync(
                SupportContext(),
                "foundation.support-context.read",
                "corr-otel-001",
                FoundationAuditDecision.Denied,
                FoundationAuditReason.AuthorizationDenied,
                supportPurpose: "case investigation");

        Assert.NotNull(result.Evidence);
        Assert.Equal(3, telemetry.Events.Count);
        Assert.Equal(
            Enum.GetValues<FoundationAuditTelemetryKind>().OrderBy(value => value),
            telemetry.Events.Select(value => value.Kind).OrderBy(value => value));
        Assert.DoesNotContain("case investigation", JsonSerializer.Serialize(telemetry.Events), StringComparison.Ordinal);
    }

    [Fact]
    public void No_production_telemetry_exporter_is_referenced()
    {
        var references = typeof(FoundationAuditCoordinator).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .ToArray();

        Assert.DoesNotContain(references, name => name!.StartsWith("OpenTelemetry", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evidence_paths_are_explicit_and_not_high_risk_or_anonymous()
    {
        var profiles = Enum.GetValues<FoundationSecurityProfile>();
        Assert.DoesNotContain(FoundationSecurityProfile.Anonymous, profiles.Where(profile =>
            profile is FoundationSecurityProfile.OrdinaryMembership
                or FoundationSecurityProfile.SupportGrant
                or FoundationSecurityProfile.PlatformGovernanceContext));
        Assert.DoesNotContain(FoundationSecurityProfile.HighRisk, profiles.Where(profile =>
            profile is FoundationSecurityProfile.OrdinaryMembership
                or FoundationSecurityProfile.SupportGrant
                or FoundationSecurityProfile.PlatformGovernanceContext));
    }

    [Fact]
    public void Tenant_owned_evidence_requires_a_trusted_context_at_factory_boundary()
    {
        Assert.Throws<ArgumentException>(() => FoundationAuditEvidenceFactory.Create(
            FoundationRequestContext.ForAuthenticatedSession(ActorA, SessionA),
            "foundation.probe.write",
            "corr-authenticated-only",
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed));
    }

    [Fact]
    public async Task Platform_evidence_is_returned_only_for_matching_governance_purpose()
    {
        var store = new LocalImmutableAuditEvidenceStore();
        await store.AppendAsync(FoundationAuditEvidenceFactory.Create(
            PlatformContext(),
            "foundation.audit.read",
            "corr-platform-002",
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed));

        var security = await store.ReadForPlatformAsync(PlatformGovernanceContextValue());
        var metadata = await store.ReadForPlatformAsync(new PlatformGovernanceContext(
            ActorA,
            PlatformGovernancePurpose.PlatformMetadata,
            new CorrelationId("corr-platform-other")));

        Assert.Single(security);
        Assert.Empty(metadata);
    }

    [Fact]
    public void Evidence_contract_does_not_expose_payload_or_secret_properties()
    {
        var propertyNames = typeof(FoundationAuditEvidence)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("Password", propertyNames);
        Assert.DoesNotContain("Token", propertyNames);
        Assert.DoesNotContain("Cookie", propertyNames);
        Assert.DoesNotContain("RawAuthorization", propertyNames);
        Assert.DoesNotContain("RequestPayload", propertyNames);
        Assert.DoesNotContain("ProviderError", propertyNames);
    }

    [Fact]
    public void Operation_and_correlation_values_have_bounded_lengths()
    {
        Assert.Throws<ArgumentException>(() => FoundationAuditEvidenceFactory.Create(
            OrdinaryContext(),
            new string('x', 129),
            "corr-bound-001",
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed));
        Assert.Throws<ArgumentException>(() => FoundationAuditEvidenceFactory.Create(
            OrdinaryContext(),
            "foundation.probe.write",
            new string('x', FoundationCorrelation.MaximumLength + 1),
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed));
    }

    [Fact]
    public async Task Capacity_exhaustion_fails_append_without_purge_or_replacement()
    {
        var store = new LocalImmutableAuditEvidenceStore(capacity: 1);
        await store.AppendAsync(FoundationAuditEvidenceFactory.Create(
            OrdinaryContext(),
            "foundation.probe.write",
            "corr-capacity-001",
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed));

        await Assert.ThrowsAsync<FoundationAuditAppendException>(async () => await store.AppendAsync(
            FoundationAuditEvidenceFactory.Create(
                OrdinaryContext(),
                "foundation.probe.write",
                "corr-capacity-002",
                FoundationAuditDecision.Allowed,
                FoundationAuditReason.Allowed)));
        Assert.Single(await store.ReadForTenantAsync(TenantContextA()));
    }

    [Fact]
    public async Task Protected_effect_runs_only_after_evidence_is_recorded()
    {
        var store = new LocalImmutableAuditEvidenceStore();
        var coordinator = CreateCoordinator(store);
        var order = new List<string>();
        var result = await coordinator.ExecuteProtectedAsync(
            OrdinaryContext(),
            "foundation.probe.write",
            "corr-order-001",
            FoundationAuditReason.Allowed,
            () =>
            {
                order.Add("effect");
                return Task.FromResult(42);
            });

        var evidence = await store.ReadForTenantAsync(TenantContextA());
        Assert.True(result.Succeeded);
        Assert.Single(evidence);
        Assert.Equal(42, result.Value);
        Assert.Equal("effect", order.Single());
    }

    [Fact]
    public async Task Denied_evidence_does_not_execute_protected_effect()
    {
        var store = new LocalImmutableAuditEvidenceStore();
        var coordinator = CreateCoordinator(store);
        var effectCalled = false;
        var denied = await coordinator.ExecuteProtectedAsync<string>(
            OrdinaryContext(),
            "foundation.target.read",
            "corr-deny-002",
            FoundationAuditReason.AuthorizationDenied,
            () =>
            {
                effectCalled = true;
                return Task.FromResult("must-not-commit");
            },
            decision: FoundationAuditDecision.Denied);

        Assert.False(effectCalled);
        Assert.False(denied.Succeeded);
        Assert.Equal("authorization_denied", denied.Code);
        Assert.Single(await store.ReadForTenantAsync(TenantContextA()));
    }

    [Fact]
    public async Task Effect_failure_preserves_allowed_evidence_and_appends_linked_safe_outcome()
    {
        var store = new LocalImmutableAuditEvidenceStore();
        var coordinator = CreateCoordinator(store);

        var result = await coordinator.ExecuteProtectedAsync<string>(
            OrdinaryContext(),
            "foundation.probe.write",
            "corr-effect-failure-001",
            FoundationAuditReason.Allowed,
            async () =>
            {
                await Task.Yield();
                throw new InvalidOperationException("provider secret must not escape");
            },
            idempotencyKey: "idem-effect-failure",
            operationVersion: "1");

        var records = await store.ReadForTenantAsync(TenantContextA());
        Assert.False(result.Succeeded);
        Assert.Equal("protected_effect_failed", result.Code);
        Assert.Equal(2, records.Count);
        Assert.Equal(FoundationAuditDecision.Allowed, records[0].Decision);
        Assert.Equal(FoundationAuditDecision.EffectFailed, records[1].Decision);
        Assert.Equal(records[0].EvidenceId, records[1].RetryOfEvidenceId);
        Assert.Equal(2, records[1].Attempt);
        Assert.DoesNotContain("provider secret", JsonSerializer.Serialize(records), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Retry_relationship_requires_attempt_two_or_later()
    {
        Assert.Throws<ArgumentException>(() => FoundationAuditEvidenceFactory.Create(
            OrdinaryContext(),
            "foundation.probe.write",
            "corr-retry-invalid",
            FoundationAuditDecision.Retry,
            FoundationAuditReason.RetryAccepted,
            retryOfEvidenceId: Guid.NewGuid(),
            attempt: 1));
    }

    [Fact]
    public async Task Tenant_context_scope_is_copied_as_trusted_organization_scope()
    {
        var scoped = FoundationRequestContext.ForTenant(
            ActorA,
            SessionA,
            TenantContext.ForOrdinaryMembership(
                TenantA,
                new MembershipReference(Guid.NewGuid()),
                new ScopeReference("company-001"),
                new CorrelationId("corr-scope-001"),
                ActorA),
            "foundation.probe.write");
        var evidence = await CreateCoordinator(new LocalImmutableAuditEvidenceStore()).RecordAsync(
            scoped,
            "foundation.probe.write",
            "corr-scope-001",
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed);

        Assert.Equal("company-001", evidence.Evidence!.OrganizationScope);
    }

    private static FoundationAuditCoordinator CreateCoordinator(LocalImmutableAuditEvidenceStore store) =>
        new(
            store,
            new LocalFoundationAuditTelemetrySink(),
            new LocalFoundationAuditOperationalSignalSink());

    private static FoundationRequestContext OrdinaryContext(TenantId? tenant = null, Guid? actor = null) =>
        FoundationRequestContext.ForTenant(
            actor ?? ActorA,
            SessionA,
            (tenant ?? TenantA) == TenantA ? TenantContextA() : TenantContextB(),
            "foundation.probe.write");

    private static FoundationRequestContext SupportContext() =>
        FoundationRequestContext.ForTenant(
            ActorA,
            SessionA,
            TenantContext.ForSupportGrant(
                TenantA,
                new SupportGrantReference(Guid.NewGuid(), Guid.NewGuid()),
                new ScopeReference("company-001"),
                new CorrelationId("corr-support-context"),
                ActorA),
            "foundation.support-context.read");

    private static FoundationRequestContext PlatformContext() =>
        FoundationRequestContext.ForPlatform(
            ActorA,
            SessionA,
            PlatformGovernanceContextValue(),
            "foundation.audit.read");

    private static TenantContext TenantContextA() =>
        TenantContext.ForOrdinaryMembership(TenantA, new MembershipReference(Guid.NewGuid()), actorId: ActorA);

    private static TenantContext TenantContextB() =>
        TenantContext.ForOrdinaryMembership(TenantB, new MembershipReference(Guid.NewGuid()), actorId: ActorB);

    private static PlatformGovernanceContext PlatformGovernanceContextValue() =>
        new(ActorA, PlatformGovernancePurpose.SecurityEvidence, new CorrelationId("corr-platform-context"));

    private static readonly Guid ActorA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ActorB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid SessionA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly TenantId TenantA = new(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"));
    private static readonly TenantId TenantB = new(Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002"));

    private sealed class FailingEvidenceSink : IFoundationAuditEvidenceSink
    {
        public ValueTask AppendAsync(FoundationAuditEvidence evidence, CancellationToken cancellationToken = default) =>
            ValueTask.FromException(new InvalidOperationException("provider error: secret must not escape"));
    }
}
