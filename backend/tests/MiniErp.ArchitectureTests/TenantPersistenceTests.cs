using System.Reflection;
using System.Text.Json;
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
    public async Task Tenant_b_reads_its_data_after_tenant_a_model_initialization_repeatedly_and_concurrently()
    {
        await using var fixture = await PersistenceFixture.CreateAsync();
        var recordA = new TenantOwnedRecord(fixture.TenantA.TenantId, "model-a");
        var recordB = new TenantOwnedRecord(fixture.TenantB.TenantId, "model-b");
        await fixture.AddAsync(fixture.TenantA, recordA);
        await fixture.AddAsync(fixture.TenantB, recordB);

        static async Task<(Guid RecordId, bool FindsTenantA)> ReadTenantBAsync(
            PersistenceFixture fixture,
            Guid tenantARecordId)
        {
            await using var session = fixture.Factory.Create(fixture.TenantB);
            var records = await session.Records.ListAsync();
            var tenantAResult = await session.Records.FindAsync(tenantARecordId);

            Assert.Single(records);
            return (records[0].Id, tenantAResult is not null);
        }

        var concurrentReads = await Task.WhenAll(
            Enumerable.Range(0, 8)
                .Select(_ => ReadTenantBAsync(fixture, recordA.Id)));

        Assert.All(concurrentReads, result =>
        {
            Assert.Equal(recordB.Id, result.RecordId);
            Assert.False(result.FindsTenantA);
        });

        await using var tenantASession = fixture.Factory.Create(fixture.TenantA);
        var tenantARecords = await tenantASession.Records.ListAsync();
        Assert.Single(tenantARecords);
        Assert.Equal(recordA.Id, tenantARecords[0].Id);
        Assert.Null(await tenantASession.Records.FindAsync(recordB.Id));
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
        var correlatedTenantA = TenantContext.ForOrdinaryMembership(
            fixture.TenantA.TenantId,
            fixture.TenantA.Membership!.Value,
            correlationId: new CorrelationId("corr-denial"));
        await using var session = fixture.Factory.Create(correlatedTenantA);
        session.Records.Add(new TenantOwnedRecord(fixture.TenantB.TenantId, "mismatch"));

        var exception = await Assert.ThrowsAsync<TenantPersistenceViolationException>(
            () => session.SaveChangesAsync());
        Assert.Equal(TenantPersistenceViolationCode.TenantContextMismatch, exception.Code);
        Assert.Equal(fixture.TenantA.TenantId, exception.TrustedTenantId);
        Assert.Equal(TenantAuthorizationPath.OrdinaryMembership, exception.AuthorizationPath);
        Assert.Equal("corr-denial", exception.CorrelationId!.Value.Value);
        Assert.Equal(TenantPersistenceOperation.Add, exception.Operation);
        Assert.Equal(TenantPersistenceDecision.Denied, exception.Decision);
        Assert.DoesNotContain(fixture.TenantB.TenantId.Value.ToString(), exception.ToString(), StringComparison.OrdinalIgnoreCase);
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

        var exception = await Assert.ThrowsAsync<TenantPersistenceViolationException>(() => session.SaveChangesAsync());
        Assert.Equal(TenantPersistenceViolationCode.TenantOwnershipMutation, exception.Code);
        Assert.Equal(fixture.TenantA.TenantId, exception.TrustedTenantId);
        Assert.Equal(TenantPersistenceOperation.Modify, exception.Operation);
    }

    [Fact]
    public async Task Legitimate_same_tenant_update_succeeds_synchronously_and_preserves_isolation()
    {
        await using var fixture = await PersistenceFixture.CreateAsync();
        var recordA = new TenantOwnedRecord(fixture.TenantA.TenantId, "sync-update-original");
        var recordB = new TenantOwnedRecord(fixture.TenantB.TenantId, "sync-update-b-original");
        await fixture.AddAsync(fixture.TenantA, recordA);
        await fixture.AddAsync(fixture.TenantB, recordB);

        await using (var session = fixture.Factory.Create(fixture.TenantA))
        {
            var typedSession = (TenantPersistenceSession)session;
            var loaded = await session.Records.FindAsync(recordA.Id);
            Assert.NotNull(loaded);
            typedSession.DbContext.Entry(loaded!).Property(nameof(TenantOwnedRecord.BusinessKey)).CurrentValue =
                "sync-update-success";

            Assert.Equal(1, typedSession.SaveChanges());
        }

        await using var tenantASession = fixture.Factory.Create(fixture.TenantA);
        var updated = await tenantASession.Records.FindAsync(recordA.Id);
        Assert.NotNull(updated);
        Assert.Equal("sync-update-success", updated!.BusinessKey);
        Assert.Equal(fixture.TenantA.TenantId, updated.TenantId);

        await using var tenantBSession = fixture.Factory.Create(fixture.TenantB);
        var untouched = await tenantBSession.Records.FindAsync(recordB.Id);
        Assert.NotNull(untouched);
        Assert.Equal("sync-update-b-original", untouched!.BusinessKey);
        Assert.Equal(fixture.TenantB.TenantId, untouched.TenantId);
    }

    [Fact]
    public async Task Legitimate_same_tenant_async_update_and_delete_succeed_without_affecting_tenant_b()
    {
        await using var fixture = await PersistenceFixture.CreateAsync();
        var recordA = new TenantOwnedRecord(fixture.TenantA.TenantId, "async-update-original");
        var recordB = new TenantOwnedRecord(fixture.TenantB.TenantId, "async-delete-b-original");
        await fixture.AddAsync(fixture.TenantA, recordA);
        await fixture.AddAsync(fixture.TenantB, recordB);

        await using (var session = fixture.Factory.Create(fixture.TenantA))
        {
            var typedSession = (TenantPersistenceSession)session;
            var loaded = await session.Records.FindAsync(recordA.Id);
            Assert.NotNull(loaded);
            typedSession.DbContext.Entry(loaded!).Property(nameof(TenantOwnedRecord.BusinessKey)).CurrentValue =
                "async-update-success";

            Assert.Equal(1, await session.SaveChangesAsync());
        }

        await using (var session = fixture.Factory.Create(fixture.TenantA))
        {
            var typedSession = (TenantPersistenceSession)session;
            var loaded = await session.Records.FindAsync(recordA.Id);
            Assert.NotNull(loaded);
            typedSession.DbContext.Entry(loaded!).State = EntityState.Deleted;

            Assert.Equal(1, await session.SaveChangesAsync());
        }

        await using var tenantASession = fixture.Factory.Create(fixture.TenantA);
        Assert.Null(await tenantASession.Records.FindAsync(recordA.Id));

        await using var tenantBSession = fixture.Factory.Create(fixture.TenantB);
        var untouched = await tenantBSession.Records.FindAsync(recordB.Id);
        Assert.NotNull(untouched);
        Assert.Equal("async-delete-b-original", untouched!.BusinessKey);
        Assert.Equal(fixture.TenantB.TenantId, untouched.TenantId);
    }

    [Fact]
    public async Task Missing_stored_row_modified_is_denied_without_provider_details()
    {
        await using var fixture = await PersistenceFixture.CreateAsync();
        await using var session = fixture.Factory.Create(fixture.TenantA);
        var unknownId = Guid.NewGuid();
        var forged = AttachForgedRecord((TenantPersistenceSession)session, unknownId, fixture.TenantA.TenantId);
        ((TenantPersistenceSession)session).DbContext.Entry(forged).State = EntityState.Modified;

        var exception = await Assert.ThrowsAsync<TenantPersistenceViolationException>(
            () => session.SaveChangesAsync());

        Assert.Equal(TenantPersistenceViolationCode.StoredOwnerUnavailable, exception.Code);
        Assert.Equal(TenantPersistenceInternalReason.StoredOwnerMissing, exception.InternalReason);
        Assert.Equal(TenantPersistenceOperation.Modify, exception.Operation);
        Assert.DoesNotContain(unknownId.ToString(), exception.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SQLite", exception.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Null(exception.InnerException);
    }

    [Fact]
    public async Task Default_key_deleted_is_denied_fail_closed()
    {
        await using var fixture = await PersistenceFixture.CreateAsync();
        await using var session = fixture.Factory.Create(fixture.TenantA);
        var forged = AttachForgedRecord((TenantPersistenceSession)session, Guid.Empty, fixture.TenantA.TenantId);
        ((TenantPersistenceSession)session).DbContext.Entry(forged).State = EntityState.Deleted;

        var exception = await Assert.ThrowsAsync<TenantPersistenceViolationException>(
            () => session.SaveChangesAsync());

        Assert.Equal(TenantPersistenceViolationCode.StoredOwnerUnavailable, exception.Code);
        Assert.Equal(TenantPersistenceInternalReason.StoredOwnerMissing, exception.InternalReason);
        Assert.Equal(TenantPersistenceOperation.Delete, exception.Operation);
    }

    [Fact]
    public async Task Missing_and_foreign_modified_or_deleted_denials_are_publicly_equivalent()
    {
        await using var fixture = await PersistenceFixture.CreateAsync();
        var recordB = new TenantOwnedRecord(fixture.TenantB.TenantId, "public-equivalence");
        await fixture.AddAsync(fixture.TenantB, recordB);

        var foreignModified = await CaptureStoredOwnerDenialAsync(
            fixture,
            recordB.Id,
            EntityState.Modified);
        var missingModified = await CaptureStoredOwnerDenialAsync(
            fixture,
            Guid.NewGuid(),
            EntityState.Modified);
        AssertPublicDenialsEquivalent(foreignModified, missingModified, recordB, TenantPersistenceOperation.Modify);
        Assert.Equal(TenantPersistenceInternalReason.StoredOwnerMismatch, foreignModified.InternalReason);
        Assert.Equal(TenantPersistenceInternalReason.StoredOwnerMissing, missingModified.InternalReason);

        var foreignDeleted = await CaptureStoredOwnerDenialAsync(
            fixture,
            recordB.Id,
            EntityState.Deleted);
        var missingDeleted = await CaptureStoredOwnerDenialAsync(
            fixture,
            Guid.NewGuid(),
            EntityState.Deleted);
        AssertPublicDenialsEquivalent(foreignDeleted, missingDeleted, recordB, TenantPersistenceOperation.Delete);
        Assert.Equal(TenantPersistenceInternalReason.StoredOwnerMismatch, foreignDeleted.InternalReason);
        Assert.Equal(TenantPersistenceInternalReason.StoredOwnerMissing, missingDeleted.InternalReason);

        Assert.DoesNotContain(
            typeof(TenantPersistenceViolationException).GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name == nameof(TenantPersistenceViolationException.InternalReason));
    }

    [Fact]
    public async Task Forged_modified_entity_cannot_overwrite_a_foreign_stored_row()
    {
        await using var fixture = await PersistenceFixture.CreateAsync();
        var recordB = new TenantOwnedRecord(fixture.TenantB.TenantId, "foreign-original");
        await fixture.AddAsync(fixture.TenantB, recordB);

        await using var session = fixture.Factory.Create(fixture.TenantA);
        var forged = AttachForgedRecord((TenantPersistenceSession)session, recordB.Id, fixture.TenantA.TenantId);
        ((TenantPersistenceSession)session).DbContext.Entry(forged).State = EntityState.Modified;

        var exception = await Assert.ThrowsAsync<TenantPersistenceViolationException>(
            () => session.SaveChangesAsync());

        Assert.Equal(TenantPersistenceViolationCode.StoredOwnerUnavailable, exception.Code);
        Assert.Equal(fixture.TenantA.TenantId, exception.TrustedTenantId);
        Assert.Equal(TenantPersistenceOperation.Modify, exception.Operation);

        await using var tenantBSession = fixture.Factory.Create(fixture.TenantB);
        var persisted = await tenantBSession.Records.FindAsync(recordB.Id);
        Assert.NotNull(persisted);
        Assert.Equal(fixture.TenantB.TenantId, persisted!.TenantId);
        Assert.Equal("foreign-original", persisted.BusinessKey);

        await using var tenantASession = fixture.Factory.Create(fixture.TenantA);
        Assert.Null(await tenantASession.Records.FindAsync(recordB.Id));
        Assert.Empty(await tenantASession.Records.ListAsync());
    }

    [Fact]
    public async Task Forged_deleted_entity_cannot_delete_a_foreign_stored_row()
    {
        await using var fixture = await PersistenceFixture.CreateAsync();
        var recordB = new TenantOwnedRecord(fixture.TenantB.TenantId, "foreign-delete-original");
        await fixture.AddAsync(fixture.TenantB, recordB);

        await using var session = fixture.Factory.Create(fixture.TenantA);
        var forged = AttachForgedRecord((TenantPersistenceSession)session, recordB.Id, fixture.TenantA.TenantId);
        ((TenantPersistenceSession)session).DbContext.Entry(forged).State = EntityState.Deleted;

        var exception = await Assert.ThrowsAsync<TenantPersistenceViolationException>(
            () => session.SaveChangesAsync());

        Assert.Equal(TenantPersistenceViolationCode.StoredOwnerUnavailable, exception.Code);
        Assert.Equal(TenantPersistenceOperation.Delete, exception.Operation);

        await using var tenantBSession = fixture.Factory.Create(fixture.TenantB);
        var persisted = await tenantBSession.Records.FindAsync(recordB.Id);
        Assert.NotNull(persisted);
        Assert.Equal(fixture.TenantB.TenantId, persisted!.TenantId);
        Assert.Equal("foreign-delete-original", persisted.BusinessKey);
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
    public async Task Every_save_changes_overload_runs_the_stored_owner_guard()
    {
        await using var fixture = await PersistenceFixture.CreateAsync();
        var recordB = new TenantOwnedRecord(fixture.TenantB.TenantId, "overload-foreign");
        await fixture.AddAsync(fixture.TenantB, recordB);

        await using (var saveChangesSession = fixture.Factory.Create(fixture.TenantA))
        {
            var typedSession = (TenantPersistenceSession)saveChangesSession;
            var forged = AttachForgedRecord(typedSession, recordB.Id, fixture.TenantA.TenantId);
            typedSession.DbContext.Entry(forged).State = EntityState.Modified;
            var exception = Assert.Throws<TenantPersistenceViolationException>(() => typedSession.SaveChanges());
            Assert.Equal(TenantPersistenceViolationCode.StoredOwnerUnavailable, exception.Code);
        }

        await using (var saveChangesBoolSession = fixture.Factory.Create(fixture.TenantA))
        {
            var typedSession = (TenantPersistenceSession)saveChangesBoolSession;
            var forged = AttachForgedRecord(typedSession, recordB.Id, fixture.TenantA.TenantId);
            typedSession.DbContext.Entry(forged).State = EntityState.Modified;
            var exception = Assert.Throws<TenantPersistenceViolationException>(
                () => typedSession.SaveChanges(acceptAllChangesOnSuccess: false));
            Assert.Equal(TenantPersistenceViolationCode.StoredOwnerUnavailable, exception.Code);
        }

        await using (var saveChangesAsyncSession = fixture.Factory.Create(fixture.TenantA))
        {
            var typedSession = (TenantPersistenceSession)saveChangesAsyncSession;
            var forged = AttachForgedRecord(typedSession, recordB.Id, fixture.TenantA.TenantId);
            typedSession.DbContext.Entry(forged).State = EntityState.Deleted;
            var exception = await Assert.ThrowsAsync<TenantPersistenceViolationException>(
                () => typedSession.SaveChangesAsync());
            Assert.Equal(TenantPersistenceViolationCode.StoredOwnerUnavailable, exception.Code);
        }

        await using (var saveChangesAsyncBoolSession = fixture.Factory.Create(fixture.TenantA))
        {
            var typedSession = (TenantPersistenceSession)saveChangesAsyncBoolSession;
            var forged = AttachForgedRecord(typedSession, recordB.Id, fixture.TenantA.TenantId);
            typedSession.DbContext.Entry(forged).State = EntityState.Deleted;
            var exception = await Assert.ThrowsAsync<TenantPersistenceViolationException>(
                () => typedSession.SaveChangesAsync(acceptAllChangesOnSuccess: false));
            Assert.Equal(TenantPersistenceViolationCode.StoredOwnerUnavailable, exception.Code);
        }
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

        var exception = await Assert.ThrowsAsync<TenantPersistenceViolationException>(
            () => session.SaveChangesAsync());
        Assert.Equal(TenantPersistenceViolationCode.PersistenceConflict, exception.Code);
        Assert.Equal(TenantPersistenceOperation.SaveChanges, exception.Operation);
        Assert.Equal(TenantPersistenceDecision.Denied, exception.Decision);
        Assert.DoesNotContain("UNIQUE", exception.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SQLite", exception.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(fixture.TenantB.TenantId.Value.ToString(), exception.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Null(exception.InnerException);
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
    public void Optional_value_objects_reject_default_wrappers_consistently()
    {
        Assert.Throws<ArgumentException>(() => TenantContext.ForOrdinaryMembership(
            CreateTenantId(),
            new MembershipReference(Guid.NewGuid()),
            default(ScopeReference),
            default(CorrelationId)));

        Assert.Throws<ArgumentException>(() => TenantContext.ForSupportGrant(
            CreateTenantId(),
            new SupportGrantReference(Guid.NewGuid(), Guid.NewGuid()),
            default(ScopeReference)));

        Assert.Throws<ArgumentException>(() => new PlatformGovernanceContext(
            Guid.NewGuid(),
            PlatformGovernancePurpose.SecurityEvidence,
            default));

        Assert.Throws<ArgumentException>(() => new TenantWorkMetadata(
            CreateTenantId(),
            TenantAuthorizationPath.OrdinaryMembership,
            new CorrelationId("corr"),
            default(ScopeReference)));
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
    public void Default_verifier_registry_explicitly_registers_current_record()
    {
        var registry = TenantOwnershipVerifierRegistry.CreateDefault();

        Assert.True(registry.TryGet(typeof(TenantOwnedRecord), out var registration));
        Assert.Equal(typeof(TenantOwnedRecord), registration.EntityType);
        Assert.False(typeof(TenantOwnershipVerifierRegistry).IsPublic);
        Assert.False(typeof(TenantOwnershipVerifierRegistration).IsPublic);
    }

    [Fact]
    public void Duplicate_verifier_registration_fails_before_model_use()
    {
        var baseline = GetDefaultTenantOwnedRegistration();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new TenantOwnershipVerifierRegistry([baseline, baseline]));

        Assert.Contains(nameof(TenantOwnedRecord), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Mapped_tenant_owned_type_without_registration_fails_model_validation()
    {
        var options = CreateSqliteOptions();
        using var context = new TestTenantPersistenceDbContext(
            options,
            CreateOrdinaryContext(),
            TenantOwnershipVerifierRegistry.CreateDefault());

        var exception = Assert.Throws<InvalidOperationException>(context.ValidateTenantOwnershipVerifiers);

        Assert.Contains(nameof(TestMappedTenantOwnedRecord), exception.Message, StringComparison.Ordinal);
        Assert.Contains("Missing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Registered_additional_mapped_tenant_owned_type_passes_model_validation()
    {
        var options = CreateSqliteOptions();
        var registry = new TenantOwnershipVerifierRegistry(
        [
            GetDefaultTenantOwnedRegistration(),
            CreateTestTenantOwnedRegistration()
        ]);
        using var context = new TestTenantPersistenceDbContext(
            options,
            CreateOrdinaryContext(),
            registry);

        context.ValidateTenantOwnershipVerifiers();
    }

    [Fact]
    public void Orphaned_verifier_registration_fails_model_validation()
    {
        var options = CreateSqliteOptions();
        var registry = new TenantOwnershipVerifierRegistry(
        [
            GetDefaultTenantOwnedRegistration(),
            CreateTestTenantOwnedRegistration()
        ]);
        using var context = new TenantPersistenceDbContext(
            options,
            CreateOrdinaryContext(),
            registry);

        var exception = Assert.Throws<InvalidOperationException>(context.ValidateTenantOwnershipVerifiers);

        Assert.Contains(nameof(TestMappedTenantOwnedRecord), exception.Message, StringComparison.Ordinal);
        Assert.Contains("Orphaned", exception.Message, StringComparison.Ordinal);
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

    private static async Task<TenantPersistenceViolationException> CaptureStoredOwnerDenialAsync(
        PersistenceFixture fixture,
        Guid recordId,
        EntityState state)
    {
        await using var session = fixture.Factory.Create(fixture.TenantA);
        var forged = AttachForgedRecord((TenantPersistenceSession)session, recordId, fixture.TenantA.TenantId);
        ((TenantPersistenceSession)session).DbContext.Entry(forged).State = state;
        return await Assert.ThrowsAsync<TenantPersistenceViolationException>(
            () => session.SaveChangesAsync());
    }

    private static void AssertPublicDenialsEquivalent(
        TenantPersistenceViolationException left,
        TenantPersistenceViolationException right,
        TenantOwnedRecord foreignRecord,
        TenantPersistenceOperation operation)
    {
        Assert.Equal(operation, left.Operation);
        Assert.Equal(operation, right.Operation);
        Assert.Equal(left.Code, right.Code);
        Assert.Equal(left.Message, right.Message);
        Assert.Equal(left.TrustedTenantId, right.TrustedTenantId);
        Assert.Equal(left.AuthorizationPath, right.AuthorizationPath);
        Assert.Equal(left.CorrelationId, right.CorrelationId);
        Assert.Equal(left.Decision, right.Decision);

        var leftExport = JsonSerializer.Serialize(new
        {
            left.Code,
            left.Message,
            left.TrustedTenantId,
            left.AuthorizationPath,
            left.CorrelationId,
            left.Operation,
            left.Decision
        });
        var rightExport = JsonSerializer.Serialize(new
        {
            right.Code,
            right.Message,
            right.TrustedTenantId,
            right.AuthorizationPath,
            right.CorrelationId,
            right.Operation,
            right.Decision
        });
        Assert.Equal(leftExport, rightExport);
        Assert.DoesNotContain(foreignRecord.Id.ToString(), leftExport, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(foreignRecord.TenantId.Value.ToString(), leftExport, StringComparison.OrdinalIgnoreCase);
        Assert.Null(left.InnerException);
        Assert.Null(right.InnerException);
        Assert.DoesNotContain("SQLite", left.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SQLite", right.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static DbContextOptions CreateSqliteOptions()
    {
        return new DbContextOptionsBuilder()
            .UseSqlite("Data Source=:memory:")
            .Options;
    }

    private static TenantOwnershipVerifierRegistration GetDefaultTenantOwnedRegistration()
    {
        var registry = TenantOwnershipVerifierRegistry.CreateDefault();
        Assert.True(registry.TryGet(typeof(TenantOwnedRecord), out var registration));
        return registration;
    }

    private static TenantOwnershipVerifierRegistration CreateTestTenantOwnedRegistration()
    {
        return new TenantOwnershipVerifierRegistration(
            typeof(TestMappedTenantOwnedRecord),
            static (_, entry) => ReadTestStoredTenantId(entry),
            static (_, entry, _) => Task.FromResult(ReadTestStoredTenantId(entry)));
    }

    private static TenantId? ReadTestStoredTenantId(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        return entry.Entity is TestMappedTenantOwnedRecord record && record.TenantId != default
            ? record.TenantId
            : null;
    }

    private static TenantOwnedRecord AttachForgedRecord(
        TenantPersistenceSession session,
        Guid foreignRecordId,
        TenantId trustedTenantId)
    {
        var forged = (TenantOwnedRecord)Activator.CreateInstance(
            typeof(TenantOwnedRecord),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: [],
            culture: null)!;
        var entry = session.DbContext.Entry(forged);
        entry.Property(nameof(TenantOwnedRecord.Id)).CurrentValue = foreignRecordId;
        entry.Property(nameof(TenantOwnedRecord.TenantId)).CurrentValue = trustedTenantId;
        entry.Property(nameof(TenantOwnedRecord.BusinessKey)).CurrentValue = "forged-payload";
        entry.Property(nameof(TenantOwnedRecord.RelationshipKind)).CurrentValue = TenantRelationshipKind.None;
        entry.Property(nameof(TenantOwnedRecord.RelatedTenantId)).CurrentValue = null;
        return forged;
    }

    private sealed class TestMappedTenantOwnedRecord : ITenantOwned
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public TenantId TenantId { get; set; }
    }

    private sealed class TestTenantPersistenceDbContext : TenantPersistenceDbContext
    {
        public TestTenantPersistenceDbContext(
            DbContextOptions options,
            TenantContext tenantContext,
            TenantOwnershipVerifierRegistry verifierRegistry)
            : base(options, tenantContext, verifierRegistry)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            var entity = modelBuilder.Entity<TestMappedTenantOwnedRecord>();
            entity.HasKey(item => item.Id);
            entity.Property(item => item.TenantId)
                .HasConversion(item => item.Value, value => new TenantId(value))
                .IsRequired();
        }
    }

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
            using (var context = new TenantPersistenceDbContext(options, tenantA))
            {
                context.Database.EnsureCreated();
            }
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
