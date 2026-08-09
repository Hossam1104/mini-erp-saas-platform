#pragma warning disable CS1591

using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.Infrastructure.Persistence.Modules.BusinessParties;

/// <summary>
/// Tenant-bound persistence adapter for M95-SL-04. The Business Parties
/// context, schema, indexes, concurrency tokens and transaction boundary are
/// owned by this module. No migration or database creation is executed here.
/// </summary>
public sealed class BusinessPartiesSupplierPersistence : ISupplierPersistence
{
    private readonly DbContextOptions options;

    internal BusinessPartiesSupplierPersistence(DbContextOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<IReadOnlyList<SupplierRecord>> ListSuppliersAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entities = await db.Suppliers
            .AsNoTracking()
            .Include(item => item.Contacts)
            .OrderBy(item => item.Code)
            .ToListAsync(cancellationToken);
        return entities.Select(ToSupplierRecord).ToArray();
    }

    public async Task<SupplierRecord?> FindSupplierAsync(
        TenantContext tenantContext,
        Guid supplierId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entity = await db.Suppliers
            .AsNoTracking()
            .Include(item => item.Contacts)
            .SingleOrDefaultAsync(item => item.Id == supplierId, cancellationToken);
        return entity is null ? null : ToSupplierRecord(entity);
    }

    public Task<MasterDataPersistenceResult<SupplierRecord>> CreateSupplierAsync(
        TenantContext tenantContext,
        Guid supplierId,
        CreateSupplierCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CommitAsync(
            tenantContext,
            evidence,
            async db =>
            {
                var codeKey = SupplierValuePolicy.ComparisonKey(command.Code);
                if (await db.Suppliers.AnyAsync(item => item.CodeKey == codeKey, cancellationToken))
                {
                    return MasterDataPersistenceResult<SupplierRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "supplier_code_duplicate");
                }

                var registrationKey = SupplierValuePolicy.NameKey(command.RegistrationReference);
                if (registrationKey is not null
                    && await db.Suppliers.AnyAsync(item => item.RegistrationKey == registrationKey, cancellationToken))
                {
                    return MasterDataPersistenceResult<SupplierRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "supplier_registration_duplicate");
                }

                if (await HasDuplicateNameAsync(
                        db,
                        command.LegalName,
                        command.TradingName,
                        excludedSupplierId: null,
                        cancellationToken: cancellationToken))
                {
                    return MasterDataPersistenceResult<SupplierRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "supplier_duplicate");
                }

                var entity = new BusinessPartiesSupplierEntity(
                    supplierId,
                    tenantContext.TenantId,
                    command.Code,
                    command.LegalName,
                    command.TradingName,
                    command.RegistrationReference);
                foreach (var contact in command.Contacts)
                {
                    entity.Contacts.Add(new BusinessPartiesSupplierContactEntity(
                        Guid.NewGuid(),
                        tenantContext.TenantId,
                        supplierId,
                        contact.Name,
                        contact.Email,
                        contact.Phone));
                }

                db.Suppliers.Add(entity);
                return MasterDataPersistenceResult<SupplierRecord>.Success(ToSupplierRecord(entity));
            },
            cancellationToken);
    }

    public Task<MasterDataPersistenceResult<SupplierRecord>> EditSupplierAsync(
        TenantContext tenantContext,
        EditSupplierCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CommitAsync(
            tenantContext,
            evidence,
            async db =>
            {
                var entity = await db.Suppliers
                    .Include(item => item.Contacts)
                    .SingleOrDefaultAsync(item => item.Id == command.SupplierId, cancellationToken);
                if (entity is null)
                {
                    return MasterDataPersistenceResult<SupplierRecord>.Denied(
                        MasterDataPersistenceOutcome.NotFound,
                        "supplier_not_found");
                }

                if (!VersionMatches(entity.Version, command.ExpectedVersion))
                {
                    return MasterDataPersistenceResult<SupplierRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "concurrency_conflict");
                }

                var codeKey = SupplierValuePolicy.ComparisonKey(command.Code);
                if (await db.Suppliers.AnyAsync(
                        item => item.Id != command.SupplierId && item.CodeKey == codeKey,
                        cancellationToken))
                {
                    return MasterDataPersistenceResult<SupplierRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "supplier_code_duplicate");
                }

                var registrationKey = SupplierValuePolicy.NameKey(command.RegistrationReference);
                if (registrationKey is not null
                    && await db.Suppliers.AnyAsync(
                        item => item.Id != command.SupplierId && item.RegistrationKey == registrationKey,
                        cancellationToken))
                {
                    return MasterDataPersistenceResult<SupplierRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "supplier_registration_duplicate");
                }

                if (await HasDuplicateNameAsync(
                        db,
                        command.LegalName,
                        command.TradingName,
                        command.SupplierId,
                        cancellationToken))
                {
                    return MasterDataPersistenceResult<SupplierRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "supplier_duplicate");
                }

                entity.Edit(
                    command.Code,
                    command.LegalName,
                    command.TradingName,
                    command.RegistrationReference);
                db.SupplierContacts.RemoveRange(entity.Contacts);
                entity.Contacts.Clear();
                foreach (var contact in command.Contacts)
                {
                    entity.Contacts.Add(new BusinessPartiesSupplierContactEntity(
                        Guid.NewGuid(),
                        tenantContext.TenantId,
                        command.SupplierId,
                        contact.Name,
                        contact.Email,
                        contact.Phone));
                }

                entity.TouchVersion();
                return MasterDataPersistenceResult<SupplierRecord>.Success(ToSupplierRecord(entity));
            },
            cancellationToken);
    }

    public Task<MasterDataPersistenceResult<SupplierRecord>> SetSupplierLifecycleAsync(
        TenantContext tenantContext,
        Guid supplierId,
        MasterDataLifecycleState lifecycleState,
        byte[] expectedVersion,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        CommitAsync(
            tenantContext,
            evidence,
            async db =>
            {
                var entity = await db.Suppliers
                    .Include(item => item.Contacts)
                    .SingleOrDefaultAsync(item => item.Id == supplierId, cancellationToken);
                if (entity is null)
                {
                    return MasterDataPersistenceResult<SupplierRecord>.Denied(
                        MasterDataPersistenceOutcome.NotFound,
                        "supplier_not_found");
                }

                if (!VersionMatches(entity.Version, expectedVersion))
                {
                    return MasterDataPersistenceResult<SupplierRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "concurrency_conflict");
                }

                if (entity.LifecycleState == lifecycleState)
                {
                    return MasterDataPersistenceResult<SupplierRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "supplier_lifecycle_no_change");
                }

                entity.SetLifecycle(lifecycleState);
                entity.TouchVersion();
                return MasterDataPersistenceResult<SupplierRecord>.Success(ToSupplierRecord(entity));
            },
            cancellationToken);

    public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(
        TenantContext tenantContext,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        CommitAsync(
            tenantContext,
            evidence,
            db =>
            {
                var entity = db.AuditEvents.Local.Single(item => item.EvidenceId == evidence.EvidenceId);
                return Task.FromResult(
                    MasterDataPersistenceResult<MasterDataAuditRecord>.Success(ToAuditRecord(entity)));
            },
            cancellationToken);

    public async Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(
        TenantContext tenantContext,
        Guid supplierId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entities = await db.AuditEvents
            .AsNoTracking()
            .Where(item => item.ResourceKind == MasterDataResourceKind.Supplier
                && item.ResourceId == supplierId)
            .ToListAsync(cancellationToken);
        return entities
            .OrderByDescending(item => item.OccurredAt)
            .Select(ToAuditRecord)
            .ToArray();
    }

    private static async Task<bool> HasDuplicateNameAsync(
        BusinessPartiesDbContext db,
        LocalizedName legalName,
        LocalizedName? tradingName,
        Guid? excludedSupplierId,
        CancellationToken cancellationToken)
    {
        var englishLegalKey = SupplierValuePolicy.NameKey(legalName.English);
        var arabicLegalKey = SupplierValuePolicy.NameKey(legalName.Arabic);
        var englishTradingKey = SupplierValuePolicy.NameKey(tradingName?.English);
        var arabicTradingKey = SupplierValuePolicy.NameKey(tradingName?.Arabic);
        return await db.Suppliers.AnyAsync(
            item => (excludedSupplierId == null || item.Id != excludedSupplierId.Value)
                && (item.EnglishLegalNameKey == englishLegalKey
                    || (arabicLegalKey != null && item.ArabicLegalNameKey == arabicLegalKey)
                    || (englishTradingKey != null && item.EnglishLegalNameKey == englishTradingKey)
                    || (arabicTradingKey != null && item.ArabicLegalNameKey == arabicTradingKey)
                    || (item.EnglishTradingNameKey != null && item.EnglishTradingNameKey == englishLegalKey)
                    || (item.ArabicTradingNameKey != null && item.ArabicTradingNameKey == arabicLegalKey)
                    || (englishTradingKey != null && item.EnglishTradingNameKey == englishTradingKey)
                    || (arabicTradingKey != null && item.ArabicTradingNameKey == arabicTradingKey)),
            cancellationToken);
    }

    private async Task<MasterDataPersistenceResult<T>> CommitAsync<T>(
        TenantContext tenantContext,
        MasterDataAuditEvidence evidence,
        Func<BusinessPartiesDbContext, Task<MasterDataPersistenceResult<T>>> effect,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(effect);

        if (evidence.Tenant.TenantId != tenantContext.TenantId.Value
            || evidence.ActorId == Guid.Empty
            || tenantContext.ActorId is { } actorId && actorId != evidence.ActorId)
        {
            return MasterDataPersistenceResult<T>.Denied(
                MasterDataPersistenceOutcome.AuditFailure,
                "audit_context_mismatch");
        }

        await using var db = CreateContext(tenantContext);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.AuditEvents.Add(new BusinessPartiesAuditEventEntity(evidence));
        try
        {
            // Append the audit row first inside the same transaction. If this
            // phase fails, the business effect has not been attempted.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await RollbackAsync(transaction);
            return MasterDataPersistenceResult<T>.Denied(
                MasterDataPersistenceOutcome.AuditFailure,
                "audit_unavailable");
        }

        MasterDataPersistenceResult<T> result;
        try
        {
            result = await effect(db);
        }
        catch (DbUpdateConcurrencyException)
        {
            await RollbackAsync(transaction);
            return MasterDataPersistenceResult<T>.Denied(
                MasterDataPersistenceOutcome.Conflict,
                "concurrency_conflict");
        }
        catch (DbUpdateException)
        {
            await RollbackAsync(transaction);
            return MasterDataPersistenceResult<T>.Denied(
                MasterDataPersistenceOutcome.Failure,
                "supplier_persistence_conflict");
        }
        catch
        {
            await RollbackAsync(transaction);
            return MasterDataPersistenceResult<T>.Denied(
                MasterDataPersistenceOutcome.Failure,
                "persistence_unavailable");
        }

        if (!result.Succeeded)
        {
            await RollbackAsync(transaction);
            return result;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            if (result.Value is SupplierRecord supplier
                && db.Suppliers.Local.SingleOrDefault(item => item.Id == supplier.Id) is { } supplierEntity)
            {
                result = result with
                {
                    Value = (T)(object)ToSupplierRecord(supplierEntity)
                };
            }
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            await RollbackAsync(transaction);
            return MasterDataPersistenceResult<T>.Denied(
                MasterDataPersistenceOutcome.Conflict,
                "concurrency_conflict");
        }
        catch (DbUpdateException)
        {
            await RollbackAsync(transaction);
            return MasterDataPersistenceResult<T>.Denied(
                MasterDataPersistenceOutcome.Failure,
                "supplier_persistence_conflict");
        }
        catch
        {
            await RollbackAsync(transaction);
            return MasterDataPersistenceResult<T>.Denied(
                MasterDataPersistenceOutcome.Failure,
                "persistence_unavailable");
        }
    }

    private BusinessPartiesDbContext CreateContext(TenantContext tenantContext) =>
        new(options, tenantContext);

    private static async Task RollbackAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync();
        }
        catch
        {
            // The original safe failure code is the evidence returned to the
            // application; rollback exceptions never cross the boundary.
        }
    }

    private static bool VersionMatches(byte[] current, byte[] expected) =>
        expected is not null && current.SequenceEqual(expected);

    private static SupplierRecord ToSupplierRecord(BusinessPartiesSupplierEntity entity) => new(
        entity.Id,
        entity.TenantId,
        entity.Code,
        entity.LegalName,
        entity.TradingName,
        entity.RegistrationReference,
        entity.LifecycleState,
        entity.Version.ToArray(),
        entity.Contacts
            .OrderBy(item => item.Name)
            .Select(item => new SupplierContactRecord(
                item.Id,
                item.TenantId,
                item.SupplierId,
                item.Name,
                item.Email,
                item.Phone,
                item.Version.ToArray()))
            .ToArray());

    private static MasterDataAuditRecord ToAuditRecord(BusinessPartiesAuditEventEntity entity)
    {
        var tenant = new TenantOwnership(entity.TenantId.Value);
        BusinessScope? scope = null;
        if (!string.IsNullOrWhiteSpace(entity.ScopePolicyId) && entity.ScopePolicyVersion > 0)
        {
            OrganizationReference? anchor = null;
            if (entity.ScopeAnchorKind is { } kind && entity.ScopeAnchorId is { } id)
            {
                anchor = new OrganizationReference(tenant, kind, id);
            }

            scope = new BusinessScope(
                tenant,
                anchor,
                new ScopePolicyReference(entity.ScopePolicyId, entity.ScopePolicyVersion));
        }

        return new MasterDataAuditRecord(
            entity.EvidenceId,
            entity.OccurredAt,
            entity.OperationId,
            entity.CorrelationId,
            entity.TenantId,
            entity.ActorId,
            entity.SessionId,
            entity.AuthorizationPath switch
            {
                FoundationAuditAuthorizationPath.OrdinaryMembership => TenantAuthorizationPath.OrdinaryMembership,
                FoundationAuditAuthorizationPath.SupportGrant => TenantAuthorizationPath.SupportGrant,
                _ => throw new InvalidOperationException("Unsupported Tenant audit path.")
            },
            entity.ResourceKind,
            entity.ResourceId,
            entity.BusinessCode,
            scope,
            entity.Operation,
            entity.PolicyOutcome,
            entity.Decision,
            entity.Reason,
            entity.BeforeSummary,
            entity.AfterSummary,
            entity.ApproverId);
    }
}

#pragma warning restore CS1591
