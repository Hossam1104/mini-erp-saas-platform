#pragma warning disable CS1591

using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.Infrastructure.Persistence.Modules.BusinessParties;

/// <summary>
/// Tenant-bound persistence adapter for M95-SL-05. Customer entities,
/// contacts, indexes, concurrency tokens, and audit transactions are owned by
/// the Business Parties module. No migration or database creation is executed
/// here.
/// </summary>
public sealed class BusinessPartiesCustomerPersistence : ICustomerPersistence, IBusinessCustomerReferenceReader
{
    private readonly DbContextOptions options;

    internal BusinessPartiesCustomerPersistence(DbContextOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<BusinessCustomerReference?> FindCustomerReferenceAsync(
        TenantContext tenantContext,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        if (customerId == Guid.Empty)
        {
            return null;
        }

        await using var db = CreateContext(tenantContext);
        var entity = await db.Customers
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == customerId, cancellationToken);
        return entity is null
            ? null
            : new BusinessCustomerReference(
                entity.Id,
                entity.TenantId,
                entity.Code,
                entity.LifecycleState);
    }

    public async Task<IReadOnlyList<CustomerRecord>> ListCustomersAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entities = await db.Customers
            .AsNoTracking()
            .Include(item => item.Contacts)
            .OrderBy(item => item.Code)
            .ToListAsync(cancellationToken);
        return entities.Select(ToCustomerRecord).ToArray();
    }

    public async Task<CustomerRecord?> FindCustomerAsync(
        TenantContext tenantContext,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entity = await db.Customers
            .AsNoTracking()
            .Include(item => item.Contacts)
            .SingleOrDefaultAsync(item => item.Id == customerId, cancellationToken);
        return entity is null ? null : ToCustomerRecord(entity);
    }

    public Task<MasterDataPersistenceResult<CustomerRecord>> CreateCustomerAsync(
        TenantContext tenantContext,
        Guid customerId,
        CreateCustomerCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CommitAsync(
            tenantContext,
            evidence,
            async db =>
            {
                var codeKey = CustomerValuePolicy.ComparisonKey(command.Code);
                if (await db.Customers.AnyAsync(item => item.CodeKey == codeKey, cancellationToken))
                {
                    return MasterDataPersistenceResult<CustomerRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "customer_code_duplicate");
                }

                if (await HasDuplicateNameAsync(
                        db,
                        command.LegalName,
                        command.TradingName,
                        excludedCustomerId: null,
                        cancellationToken: cancellationToken))
                {
                    return MasterDataPersistenceResult<CustomerRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "customer_duplicate");
                }

                var entity = new BusinessPartiesCustomerEntity(
                    customerId,
                    tenantContext.TenantId,
                    command.Code,
                    command.LegalName,
                    command.TradingName);
                foreach (var contact in command.Contacts)
                {
                    entity.Contacts.Add(new BusinessPartiesCustomerContactEntity(
                        Guid.NewGuid(),
                        tenantContext.TenantId,
                        customerId,
                        contact.Name,
                        contact.Email,
                        contact.Phone));
                }

                db.Customers.Add(entity);
                return MasterDataPersistenceResult<CustomerRecord>.Success(ToCustomerRecord(entity));
            },
            cancellationToken);
    }

    public Task<MasterDataPersistenceResult<CustomerRecord>> EditCustomerAsync(
        TenantContext tenantContext,
        EditCustomerCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CommitAsync(
            tenantContext,
            evidence,
            async db =>
            {
                var entity = await db.Customers
                    .Include(item => item.Contacts)
                    .SingleOrDefaultAsync(item => item.Id == command.CustomerId, cancellationToken);
                if (entity is null)
                {
                    return MasterDataPersistenceResult<CustomerRecord>.Denied(
                        MasterDataPersistenceOutcome.NotFound,
                        "customer_not_found");
                }

                if (!VersionMatches(entity.Version, command.ExpectedVersion))
                {
                    return MasterDataPersistenceResult<CustomerRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "concurrency_conflict");
                }

                var codeKey = CustomerValuePolicy.ComparisonKey(command.Code);
                if (await db.Customers.AnyAsync(
                        item => item.Id != command.CustomerId && item.CodeKey == codeKey,
                        cancellationToken))
                {
                    return MasterDataPersistenceResult<CustomerRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "customer_code_duplicate");
                }

                if (await HasDuplicateNameAsync(
                        db,
                        command.LegalName,
                        command.TradingName,
                        command.CustomerId,
                        cancellationToken))
                {
                    return MasterDataPersistenceResult<CustomerRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "customer_duplicate");
                }

                entity.Edit(
                    command.Code,
                    command.LegalName,
                    command.TradingName);
                db.CustomerContacts.RemoveRange(entity.Contacts);
                entity.Contacts.Clear();
                foreach (var contact in command.Contacts)
                {
                    entity.Contacts.Add(new BusinessPartiesCustomerContactEntity(
                        Guid.NewGuid(),
                        tenantContext.TenantId,
                        command.CustomerId,
                        contact.Name,
                        contact.Email,
                        contact.Phone));
                }

                entity.TouchVersion();
                return MasterDataPersistenceResult<CustomerRecord>.Success(ToCustomerRecord(entity));
            },
            cancellationToken);
    }

    public Task<MasterDataPersistenceResult<CustomerRecord>> SetCustomerLifecycleAsync(
        TenantContext tenantContext,
        Guid customerId,
        MasterDataLifecycleState lifecycleState,
        byte[] expectedVersion,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        CommitAsync(
            tenantContext,
            evidence,
            async db =>
            {
                var entity = await db.Customers
                    .Include(item => item.Contacts)
                    .SingleOrDefaultAsync(item => item.Id == customerId, cancellationToken);
                if (entity is null)
                {
                    return MasterDataPersistenceResult<CustomerRecord>.Denied(
                        MasterDataPersistenceOutcome.NotFound,
                        "customer_not_found");
                }

                if (!VersionMatches(entity.Version, expectedVersion))
                {
                    return MasterDataPersistenceResult<CustomerRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "concurrency_conflict");
                }

                if (entity.LifecycleState == lifecycleState)
                {
                    return MasterDataPersistenceResult<CustomerRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "customer_lifecycle_no_change");
                }

                entity.SetLifecycle(lifecycleState);
                entity.TouchVersion();
                return MasterDataPersistenceResult<CustomerRecord>.Success(ToCustomerRecord(entity));
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
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entities = await db.AuditEvents
            .AsNoTracking()
            .Where(item => item.ResourceKind == MasterDataResourceKind.BusinessCustomer
                && item.ResourceId == customerId)
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
        Guid? excludedCustomerId,
        CancellationToken cancellationToken)
    {
        var englishLegalKey = CustomerValuePolicy.NameKey(legalName.English);
        var arabicLegalKey = CustomerValuePolicy.NameKey(legalName.Arabic);
        var englishTradingKey = CustomerValuePolicy.NameKey(tradingName?.English);
        var arabicTradingKey = CustomerValuePolicy.NameKey(tradingName?.Arabic);
        return await db.Customers.AnyAsync(
            item => (excludedCustomerId == null || item.Id != excludedCustomerId.Value)
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
            // Audit is appended before the business effect in the same
            // transaction. A failed audit never permits a false success.
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
                "customer_persistence_conflict");
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
            if (result.Value is CustomerRecord customer
                && db.Customers.Local.SingleOrDefault(item => item.Id == customer.Id) is { } customerEntity)
            {
                result = result with
                {
                    Value = (T)(object)ToCustomerRecord(customerEntity)
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
                "customer_persistence_conflict");
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

    private static async Task RollbackAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync();
        }
        catch
        {
            // Preserve the original safe failure code.
        }
    }

    private static bool VersionMatches(byte[] current, byte[] expected) =>
        expected is not null && current.SequenceEqual(expected);

    private static CustomerRecord ToCustomerRecord(BusinessPartiesCustomerEntity entity) => new(
        entity.Id,
        entity.TenantId,
        entity.Code,
        entity.LegalName,
        entity.TradingName,
        entity.LifecycleState,
        entity.Version.ToArray(),
        entity.Contacts
            .OrderBy(item => item.Name)
            .Select(item => new CustomerContactRecord(
                item.Id,
                item.TenantId,
                item.CustomerId,
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
