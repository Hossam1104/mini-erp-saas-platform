using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Infrastructure.Persistence;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class TenantPersistenceTests
{
    [Fact]
    public void Ordinary_membership_context_is_valid()
    {
        var context = CreateOrdinaryContext();

        Assert.Equal(TenantAuthorizationPath.OrdinaryMembership, context.AuthorizationPath);
        Assert.NotEqual(default, context.TenantId);
        Assert.NotNull(context.Membership);
        Assert.Null(context.SupportGrant);
    }

    [Fact]
    public void Support_grant_context_is_valid()
    {
        var context = CreateSupportContext();

        Assert.Equal(TenantAuthorizationPath.SupportGrant, context.AuthorizationPath);
        Assert.NotEqual(default, context.TenantId);
        Assert.Null(context.Membership);
        Assert.NotNull(context.SupportGrant);
    }

    [Fact]
    public void Both_authorization_paths_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => TenantContext.CreateValidated(
            CreateTenantId(),
            TenantAuthorizationPath.OrdinaryMembership,
            new MembershipReference(Guid.NewGuid()),
            new SupportGrantReference(Guid.NewGuid(), Guid.NewGuid())));
    }

    [Fact]
    public void Neither_authorization_path_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => TenantContext.CreateValidated(
            CreateTenantId(),
            TenantAuthorizationPath.OrdinaryMembership,
            membership: null,
            supportGrant: null));
    }

    [Fact]
    public void Unknown_authorization_path_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TenantContext.CreateValidated(
            CreateTenantId(),
            (TenantAuthorizationPath)99,
            new MembershipReference(Guid.NewGuid()),
            supportGrant: null));
    }

    [Fact]
    public void Default_tenant_id_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new TenantId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => TenantContext.ForOrdinaryMembership(
            default,
            new MembershipReference(Guid.NewGuid())));
    }

    [Fact]
    public void Tenant_context_is_immutable()
    {
        var writableProperties = typeof(TenantContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanWrite)
            .ToArray();

        Assert.Empty(writableProperties);
    }

    [Fact]
    public void Platform_governance_context_is_not_a_tenant_path()
    {
        Assert.False(typeof(TenantContext).IsAssignableFrom(typeof(PlatformGovernanceContext)));
        Assert.False(typeof(PlatformGovernanceContext).IsAssignableFrom(typeof(TenantContext)));
    }

    [Fact]
    public async Task Tenant_a_reads_only_tenant_a_data()
    {
        await using var fixture = await PersistenceFixture.CreateAsync();
        var recordA = new TenantOwnedRecord(fixture.TenantA.TenantId, "same-key");
        var recordB = new TenantOwnedRecord(fixture.TenantB.TenantId, "same-key");

        await fixture.AddAsync(fixture.TenantA, recordA);
        await fixture.AddAsync(fixture.TenantB, recordB);

        await using var tenantASession = fixture.Factory.Create(fixture.TenantA);
        var records = await tenantASession.Records.ListAsync();

        Assert.Single(records);
        Assert.Equal(recordA.Id, records[0].Id);
    }

    [Fact]
    public async Task Tenant_a_cannot_read_tenant_b_data()
    {
        await using var fixture = await PersistenceFixture.CreateAsync();
        var recordB = new TenantOwnedRecord(fixture.TenantB.TenantId, "tenant-b-only");
        await fixture.AddAsync(fixture.TenantB, recordB);

        await using var tenantASession = fixture.Factory.Create(fixture.TenantA);

        Assert.Empty(await tenantASession.Records.ListAsync());
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_no_target_existence()
    {
        await using var fixture = await PersistenceFixture.CreateAsync();
        var recordB = new TenantOwnedRecord(fixture.TenantB.TenantId, "hidden");
        await fixture.AddAsync(fixture.TenantB, recordB);

        await using var tenantASession = fixture.Factory.Create(fixture.TenantA);

        Assert.Null(await tenantASession.Records.FindAsync(recordB.Id));
    }

    [Fact]
    public void Ordinary_repository_has_no_unscoped_read_path()
    {
        var methods = typeof(ITenantRecordRepository).GetMethods(BindingFlags.Public | BindingFlags.Instance);

        Assert.DoesNotContain(methods, method => method.Name.Contains("Unscoped", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methods, method => method.GetParameters().Any(parameter =>
            parameter.ParameterType == typeof(TenantId?) || parameter.Name?.Contains("tenant", StringComparison.OrdinalIgnoreCase) == true));
    }

    [Fact]
    public async Task Tenant_a_can_write_tenant_a_data()
    {
        await using var fixture = await PersistenceFixture.CreateAsync();
        var record = new TenantOwnedRecord(fixture.TenantA.TenantId, "owned-by-a");

        await fixture.AddAsync(fixture.TenantA, record);

        await using var session = fixture.Factory.Create(fixture.TenantA);
        Assert.NotNull(await session.Records.FindAsync(record.Id));
    }

    [Fact]
    public async Task Tenant_a_cannot_create_tenant_b_owned_data()
    {
        await using var fixture = await PersistenceFixture.CreateAsync();
        await using var session = fixture.Factory.Create(fixture.TenantA);
        session.Records.Add(new TenantOwnedRecord(fixture.TenantB.TenantId, "wrong-owner"));

        await Assert.ThrowsAsync<TenantPersistenceViolationException>(() => session.SaveChangesAsync());
    }

    [Fact]
    public async Task Missing_tenant_id_is_rejected_before_persistence()
    {
        await using var fixture = await PersistenceFixture.CreateAsync();
        await using var session = fixture.Factory.Create(fixture.TenantA);
        var missingTenantRecord = (TenantOwnedRecord)Activator.CreateInstance(
            typeof(TenantOwnedRecord),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: [],
            culture: null)!;
        session.Records.Add(missingTenantRecord);

        await Assert.ThrowsAsync<TenantPersistenceViolationException>(() => session.SaveChangesAsync());
    }

    [Fact]
    public async Task Mismatched_tenant_id_is_rejected_before_persistence()
    {
        await using var fixture = await PersistenceFixture.CreateAsync();
        await using var session = fixture.Factory.Create(fixture.TenantA);
        session.Records.Add(new TenantOwnedRecord(fixture.TenantB.TenantId, "mismatch"));

        await Assert.ThrowsAsync<TenantPersistenceViolationException>(() => session.SaveChangesAsync());
    }

    [Fact]
    public async Task Tenant_ownership_cannot_change_after_establishment()
    {
        await using var fixture = await PersistenceFixture.CreateAsync();
        var record = new TenantOwnedRecord(fixture.TenantA.TenantId, "immutable-owner");
        await fixture.AddAsync(fixture.TenantA, record);

        await using var session = fixture.Factory.Create(fixture.TenantA);
        var loaded = await session.Records.FindAsync(record.Id);
        Assert.NotNull(loaded);
        ((TenantPersistenceSession)session).DbContext.Entry(loaded!).Property(nameof(TenantOwnedRecord.TenantId)).CurrentValue = fixture.TenantB.TenantId;

        await Assert.ThrowsAsync<TenantPersistenceViolationException>(() => session.SaveChangesAsync());
    }

    [Fact]
    public void Platform_governance_cannot_create_a_tenant_session()
    {
        var createMethods = typeof(TenantPersistenceSessionFactory)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.Name == nameof(TenantPersistenceSessionFactory.Create))
            .ToArray();

        Assert.Single(createMethods);
        Assert.Equal(typeof(TenantContext), createMethods[0].GetParameters()[0].ParameterType);
    }

    [Fact]
    public void Mixed_authorization_context_cannot_persist()
    {
        Assert.Throws<ArgumentException>(() => TenantContext.CreateValidated(
            CreateTenantId(),
            TenantAuthorizationPath.SupportGrant,
            new MembershipReference(Guid.NewGuid()),
            new SupportGrantReference(Guid.NewGuid(), Guid.NewGuid())));
    }

    [Fact]
    public async Task Same_tenant_relationship_is_accepted()
    {
        await using var fixture = await PersistenceFixture.CreateAsync();
        var record = new TenantOwnedRecord(
            fixture.TenantA.TenantId,
            "same-tenant-relationship",
            TenantRelationshipKind.CompanyBranch,
            fixture.TenantA.TenantId);

        await fixture.AddAsync(fixture.TenantA, record);

        await using var session = fixture.Factory.Create(fixture.TenantA);
        Assert.NotNull(await session.Records.FindAsync(record.Id));
    }

    [Theory]
    [InlineData(TenantRelationshipKind.CompanyBranch)]
    [InlineData(TenantRelationshipKind.BranchWarehouse)]
    [InlineData(TenantRelationshipKind.CompanyDepartment)]
    [InlineData(TenantRelationshipKind.MembershipTenant)]
    [InlineData(TenantRelationshipKind.RoleAssignmentTenant)]
    [InlineData(TenantRelationshipKind.AccessScopeGrantRoleAssignment)]
    [InlineData(TenantRelationshipKind.SupportCaseGrant)]
    [InlineData(TenantRelationshipKind.TenantFile)]
    [InlineData(TenantRelationshipKind.TenantExport)]
    [InlineData(TenantRelationshipKind.DurableWork)]
    public async Task Cross_tenant_relationship_is_rejected(TenantRelationshipKind relationshipKind)
    {
        await using var fixture = await PersistenceFixture.CreateAsync();
        await using var session = fixture.Factory.Create(fixture.TenantA);
        session.Records.Add(new TenantOwnedRecord(
            fixture.TenantA.TenantId,
            $"cross-{relationshipKind}",
            relationshipKind,
            fixture.TenantB.TenantId));

        await Assert.ThrowsAsync<TenantPersistenceViolationException>(() => session.SaveChangesAsync());
    }

    [Fact]
    public async Task Role_assignment_and_grant_tenant_mismatch_is_rejected()
    {
        await using var fixture = await PersistenceFixture.CreateAsync();
        await using var session = fixture.Factory.Create(fixture.TenantA);
        session.Records.Add(new TenantOwnedRecord(
            fixture.TenantA.TenantId,
            "grant-mismatch",
            TenantRelationshipKind.AccessScopeGrantRoleAssignment,
            fixture.TenantB.TenantId));

        await Assert.ThrowsAsync<TenantPersistenceViolationException>(() => session.SaveChangesAsync());
    }

    [Fact]
    public async Task Same_business_value_is_allowed_in_different_tenants()
    {
        await using var fixture = await PersistenceFixture.CreateAsync();
        await fixture.AddAsync(fixture.TenantA, new TenantOwnedRecord(fixture.TenantA.TenantId, "duplicate-safe"));
        await fixture.AddAsync(fixture.TenantB, new TenantOwnedRecord(fixture.TenantB.TenantId, "duplicate-safe"));

        await using var session = fixture.Factory.Create(fixture.TenantA);
        Assert.Single(await session.Records.ListAsync());
    }

    [Fact]
    public async Task Duplicate_business_value_in_same_tenant_is_rejected()
    {
        await using var fixture = await PersistenceFixture.CreateAsync();
        await fixture.AddAsync(fixture.TenantA, new TenantOwnedRecord(fixture.TenantA.TenantId, "duplicate"));
        await using var session = fixture.Factory.Create(fixture.TenantA);
        session.Records.Add(new TenantOwnedRecord(fixture.TenantA.TenantId, "duplicate"));

        await Assert.ThrowsAsync<DbUpdateException>(() => session.SaveChangesAsync());
    }

    [Fact]
    public void Privileged_request_requires_explicit_governance_context_and_purpose()
    {
        var context = new PlatformGovernanceContext(
            Guid.NewGuid(),
            PlatformGovernancePurpose.PlatformMetadata,
            new CorrelationId("corr-privileged"));
        var request = new PrivilegedPersistenceRequest(
            context,
            PrivilegedPersistencePurpose.Maintenance,
            context.CorrelationId);

        Assert.Same(context, request.GovernanceContext);
        Assert.Equal(PrivilegedPersistencePurpose.Maintenance, request.Purpose);
    }

    [Fact]
    public void Durable_work_metadata_rejects_missing_tenant_or_correlation()
    {
        Assert.Throws<ArgumentException>(() => new TenantWorkMetadata(
            default,
            TenantAuthorizationPath.OrdinaryMembership,
            new CorrelationId("corr")));

        Assert.Throws<ArgumentException>(() => new TenantWorkMetadata(
            CreateTenantId(),
            TenantAuthorizationPath.OrdinaryMembership,
            default));
    }

    [Fact]
    public void Ordinary_application_cannot_resolve_privileged_boundary()
    {
        Assert.False(typeof(IPrivilegedPersistenceBoundary).IsPublic);
        Assert.DoesNotContain(
            typeof(TenantPersistenceSessionFactory).GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name.Contains("Unscoped", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void No_public_ignore_filter_switch_exists()
    {
        var publicTypes = typeof(TenantPersistenceSessionFactory).Assembly.GetExportedTypes();

        Assert.DoesNotContain(
            publicTypes.SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)),
            property => property.Name.Contains("IgnoreTenant", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("IgnoreQueryFilter", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            publicTypes.SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)),
            method => method.Name.Contains("IgnoreQueryFilter", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Infrastructure_dependency_direction_is_one_way()
    {
        var infrastructureReferences = typeof(TenantPersistenceSessionFactory).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .ToArray();

        Assert.Contains("MiniErp.App", infrastructureReferences);
        Assert.DoesNotContain("MiniErp.Api", infrastructureReferences);
    }

    [Fact]
    public void Application_has_no_infrastructure_reference()
    {
        var references = typeof(TenantContext).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, reference => reference.Name == "MiniErp.Infrastructure");
    }

    [Fact]
    public void Platform_context_is_not_a_substitute_for_tenant_context()
    {
        var factoryMethods = typeof(ITenantPersistenceSessionFactory)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance);

        Assert.DoesNotContain(
            factoryMethods.SelectMany(method => method.GetParameters()),
            parameter => parameter.ParameterType == typeof(PlatformGovernanceContext));
    }

    [Fact]
    public void Durable_work_metadata_is_immutable_and_tenant_bound()
    {
        var metadata = new TenantWorkMetadata(
            CreateTenantId(),
            TenantAuthorizationPath.SupportGrant,
            new CorrelationId("corr-work"),
            new ScopeReference("company-1"),
            Guid.NewGuid());

        Assert.NotEqual(default, metadata.TenantId);
        Assert.Equal(TenantAuthorizationPath.SupportGrant, metadata.AuthorizationPath);
        Assert.DoesNotContain(
            typeof(TenantWorkMetadata)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.CanWrite);
    }

    private static TenantId CreateTenantId() => new(Guid.NewGuid());

    private static TenantContext CreateOrdinaryContext() => TenantContext.ForOrdinaryMembership(
        CreateTenantId(),
        new MembershipReference(Guid.NewGuid()),
        new ScopeReference("tenant"));

    private static TenantContext CreateSupportContext() => TenantContext.ForSupportGrant(
        CreateTenantId(),
        new SupportGrantReference(Guid.NewGuid(), Guid.NewGuid()),
        new ScopeReference("case-scope"));

    private sealed class PersistenceFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private PersistenceFixture(
            SqliteConnection connection,
            TenantPersistenceSessionFactory factory,
            TenantContext tenantA,
            TenantContext tenantB)
        {
            _connection = connection;
            Factory = factory;
            TenantA = tenantA;
            TenantB = tenantB;
        }

        public TenantPersistenceSessionFactory Factory { get; }

        public TenantContext TenantA { get; }

        public TenantContext TenantB { get; }

        public static async Task<PersistenceFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder()
                .UseSqlite(connection)
                .Options;
            var tenantA = CreateOrdinaryContext();
            var tenantB = TenantContext.ForSupportGrant(
                CreateTenantId(),
                new SupportGrantReference(Guid.NewGuid(), Guid.NewGuid()));
            TenantPersistenceSessionFactory.EnsureCreatedForTests(options, tenantA);
            return new PersistenceFixture(
                connection,
                new TenantPersistenceSessionFactory(options),
                tenantA,
                tenantB);
        }

        public async Task AddAsync(TenantContext context, TenantOwnedRecord record)
        {
            await using var session = Factory.Create(context);
            session.Records.Add(record);
            await session.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();
        }
    }
}
