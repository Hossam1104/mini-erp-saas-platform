using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.MasterData;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Contracts.Modules.Procurement;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class PurchaseInvoiceMatchingRemediationTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid ExchangeRateId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa1111");
    private static readonly Guid ExchangeRateVersion1Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa1112");
    private static readonly Guid ExchangeRateVersion2Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa1113");
    private static readonly Guid CompanyA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa2001");

    [Fact]
    public async Task Mesp120_provider_resolves_the_active_effective_version_and_returns_server_owned_snapshot()
    {
        var provider = new MasterDataPurchaseInvoiceMatchingExchangeRateReferenceProvider(
            new StubExchangeRatePersistence(ExchangeRate(TenantA, MasterDataLifecycleState.Active)));

        var result = await provider.ResolveAsync(
            Context(TenantA),
            ExchangeRateId,
            "EUR",
            "USD",
            new DateOnly(2026, 8, 20),
            null);

        Assert.True(result.Succeeded, result.Code);
        Assert.Equal(ExchangeRateId, result.Value!.ExchangeRateId);
        Assert.Equal(ExchangeRateVersion2Id, result.Value.ExchangeRateVersionId);
        Assert.Equal(2, result.Value.VersionNumber);
        Assert.Equal(1.25m, result.Value.Rate);
        Assert.Equal(100, result.Value.Scale);
        Assert.Equal("Configured", result.Value.Provenance);
        Assert.Equal("MESP-120-master-data", result.Value.Source);
        Assert.Equal(new DateOnly(2026, 8, 20), result.Value.EffectiveOn);
    }

    [Fact]
    public async Task Mesp120_provider_rejects_foreign_tenant_exchange_rate_id()
    {
        var provider = new MasterDataPurchaseInvoiceMatchingExchangeRateReferenceProvider(
            new StubExchangeRatePersistence(ExchangeRate(TenantB, MasterDataLifecycleState.Active)));

        var result = await provider.ResolveAsync(Context(TenantA), ExchangeRateId, "EUR", "USD", new DateOnly(2026, 8, 20), null);

        Assert.False(result.Succeeded);
        Assert.Equal("currency_not_comparable", result.Code);
    }

    [Fact]
    public async Task Mesp120_provider_fails_closed_for_wrong_pair_inactive_rate_and_missing_effective_version()
    {
        var wrongPair = new MasterDataPurchaseInvoiceMatchingExchangeRateReferenceProvider(
            new StubExchangeRatePersistence(ExchangeRate(TenantA, MasterDataLifecycleState.Active) with { SourceCurrencyCode = "GBP" }));
        var inactive = new MasterDataPurchaseInvoiceMatchingExchangeRateReferenceProvider(
            new StubExchangeRatePersistence(ExchangeRate(TenantA, MasterDataLifecycleState.Inactive)));
        var missingDate = new MasterDataPurchaseInvoiceMatchingExchangeRateReferenceProvider(
            new StubExchangeRatePersistence(ExchangeRate(TenantA, MasterDataLifecycleState.Active, onlyHistorical: true)));

        Assert.False((await wrongPair.ResolveAsync(Context(TenantA), ExchangeRateId, "EUR", "USD", new DateOnly(2026, 8, 20), null)).Succeeded);
        Assert.False((await inactive.ResolveAsync(Context(TenantA), ExchangeRateId, "EUR", "USD", new DateOnly(2026, 8, 20), null)).Succeeded);
        Assert.False((await missingDate.ResolveAsync(Context(TenantA), ExchangeRateId, "EUR", "USD", new DateOnly(2026, 8, 20), null)).Succeeded);
    }

    [Fact]
    public async Task Resolution_default_is_not_different_actor_but_configured_policy_is()
    {
        var scope = new PurchaseRequestScope(TenantA, CompanyA, null);
        var defaultPolicy = await new DefaultPurchaseInvoiceMatchingResolutionPolicyProvider()
            .ResolveAsync(scope, DateTimeOffset.UtcNow);
        var configuredPolicy = await new ConfiguredPurchaseInvoiceMatchingResolutionPolicyProvider(
        [
            new PurchaseInvoiceMatchingResolutionPolicyBinding(
                scope,
                new PurchaseInvoiceMatchingResolutionPolicyDefinition("sod", 2, true, true, true, DateTimeOffset.MinValue, null))
        ]).ResolveAsync(scope, DateTimeOffset.UtcNow);

        Assert.False(defaultPolicy.RequireDifferentActor);
        Assert.True(configuredPolicy.RequireDifferentActor);
    }

    [Fact]
    public void Matching_exchange_rate_request_contains_only_a_server_owned_reference()
    {
        var propertyNames = typeof(PurchaseInvoiceExchangeRateReferenceRequest)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.Contains(nameof(PurchaseInvoiceExchangeRateReferenceRequest.ExchangeRateId), propertyNames);
        Assert.Contains(nameof(PurchaseInvoiceExchangeRateReferenceRequest.EffectiveOn), propertyNames);
        Assert.DoesNotContain("Rate", propertyNames);
        Assert.DoesNotContain("Scale", propertyNames);
        Assert.DoesNotContain("Source", propertyNames);
        Assert.DoesNotContain("Version", propertyNames);
    }

    private static MasterDataExchangeRateRecord ExchangeRate(Guid tenantId, MasterDataLifecycleState lifecycleState, bool onlyHistorical = false)
    {
        IReadOnlyList<MasterDataExchangeRateVersionRecord> versions = onlyHistorical
            ? new[] { new MasterDataExchangeRateVersionRecord(ExchangeRateVersion1Id, 1, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), 1.2m, 100, ExchangeRateProvenance.Manual, "Historical", "EUR", "USD") }
            :
            new[]
            {
                new MasterDataExchangeRateVersionRecord(ExchangeRateVersion1Id, 1, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30), 1.2m, 100, ExchangeRateProvenance.Manual, "Prior", "EUR", "USD"),
                new MasterDataExchangeRateVersionRecord(ExchangeRateVersion2Id, 2, new DateOnly(2026, 7, 1), null, 1.25m, 100, ExchangeRateProvenance.Configured, "MESP-120-master-data", "EUR", "USD")
            };
        return new MasterDataExchangeRateRecord(
            ExchangeRateId,
            new TenantId(tenantId),
            CompanyA,
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa2002"),
            "EUR",
            "USD",
            lifecycleState,
            versions.Max(item => item.VersionNumber),
            versions,
            [1]);
    }

    private static TenantContext Context(Guid tenantId) => TenantContext.ForOrdinaryMembership(
        new TenantId(tenantId),
        new MembershipReference(Guid.NewGuid()),
        new ScopeReference($"Tenant:{tenantId:D}"),
        new CorrelationId($"fx-{Guid.NewGuid():N}"),
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa3001"));

    private sealed class StubExchangeRatePersistence(MasterDataExchangeRateRecord record) : IMasterDataExchangeRatePersistence
    {
        public Task<IReadOnlyList<MasterDataExchangeRateRecord>> ListExchangeRatesAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MasterDataExchangeRateRecord>>([record]);

        public Task<MasterDataExchangeRateRecord?> FindExchangeRateAsync(TenantContext tenantContext, Guid exchangeRateId, CancellationToken cancellationToken = default) =>
            Task.FromResult<MasterDataExchangeRateRecord?>(record.Id == exchangeRateId ? record : null);

        public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> CreateExchangeRateAsync(TenantContext tenantContext, Guid exchangeRateId, CreateMasterDataExchangeRateCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unsupported<MasterDataPersistenceResult<MasterDataExchangeRateRecord>>();
        public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> EditExchangeRateAsync(TenantContext tenantContext, EditMasterDataExchangeRateCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unsupported<MasterDataPersistenceResult<MasterDataExchangeRateRecord>>();
        public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> SetExchangeRateLifecycleAsync(TenantContext tenantContext, Guid exchangeRateId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unsupported<MasterDataPersistenceResult<MasterDataExchangeRateRecord>>();
        public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unsupported<MasterDataPersistenceResult<MasterDataAuditRecord>>();
        public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, Guid? exchangeRateId = null, CancellationToken cancellationToken = default) => Unsupported<IReadOnlyList<MasterDataAuditRecord>>();

        private static Task<T> Unsupported<T>() => Task.FromException<T>(new NotSupportedException());
    }
}
