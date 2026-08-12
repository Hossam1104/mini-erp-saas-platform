#pragma warning disable CS1591

using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.Infrastructure.Persistence.Modules.MasterData;

/// <summary>
/// Tenant-filtered Tax identity and rate-version persistence. The adapter
/// keeps the Tax aggregate and its audit evidence in one transaction and does
/// not expose the module-owned DbContext to callers.
/// </summary>
public sealed class MasterDataTaxPersistence : IMasterDataTaxPersistence
{
    private readonly DbContextOptions options;

    internal MasterDataTaxPersistence(DbContextOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<IReadOnlyList<MasterDataTaxRecord>> ListTaxesAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entities = await db.Taxes
            .AsNoTracking()
            .Include(item => item.RateVersions)
            .OrderBy(item => item.Code)
            .ToListAsync(cancellationToken);
        return entities.Select(ToTaxRecord).ToArray();
    }

    public async Task<MasterDataTaxRecord?> FindTaxAsync(
        TenantContext tenantContext,
        Guid taxId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entity = await db.Taxes
            .AsNoTracking()
            .Include(item => item.RateVersions)
            .SingleOrDefaultAsync(item => item.Id == taxId, cancellationToken);
        return entity is null ? null : ToTaxRecord(entity);
    }

    public Task<MasterDataPersistenceResult<MasterDataTaxRecord>> CreateTaxAsync(
        TenantContext tenantContext,
        Guid taxId,
        CreateMasterDataTaxCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CommitAsync(
            tenantContext,
            evidence,
            "tax_duplicate",
            async db =>
            {
                var nameKey = MasterDataTaxValuePolicy.NameKey(command.Name);
                if (await db.Taxes.AnyAsync(
                    item => item.CodeKey == command.Code
                        || item.NameKey == nameKey,
                    cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataTaxRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "tax_duplicate");
                }

                var entity = new MasterDataTaxEntity(
                    taxId,
                    tenantContext.TenantId,
                    command.Code,
                    command.CategoryCode,
                    command.CategoryName,
                    command.Name,
                    command.Applicability);
                var version = CreateVersionEntity(
                    tenantContext.TenantId,
                    taxId,
                    1,
                    command.RateVersion);
                entity.RateVersions.Add(version);
                db.Taxes.Add(entity);
                db.TaxRateVersions.Add(version);
                return MasterDataPersistenceResult<MasterDataTaxRecord>.Success(
                    ToTaxRecord(entity, [version]));
            },
            cancellationToken);
    }

    public Task<MasterDataPersistenceResult<MasterDataTaxRecord>> EditTaxAsync(
        TenantContext tenantContext,
        EditMasterDataTaxCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CommitAsync(
            tenantContext,
            evidence,
            "tax_duplicate",
            async db =>
            {
                var entity = await db.Taxes.SingleOrDefaultAsync(
                    item => item.Id == command.TaxId,
                    cancellationToken);
                if (entity is null)
                {
                    return MasterDataPersistenceResult<MasterDataTaxRecord>.Denied(
                        MasterDataPersistenceOutcome.NotFound,
                        "tax_not_found");
                }

                if (!VersionMatches(entity.Version, command.ExpectedVersion))
                {
                    return MasterDataPersistenceResult<MasterDataTaxRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "concurrency_conflict");
                }

                var nameKey = MasterDataTaxValuePolicy.NameKey(command.Name);
                if (await db.Taxes.AnyAsync(
                    item => item.Id != command.TaxId
                        && (item.CodeKey == command.Code || item.NameKey == nameKey),
                    cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataTaxRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "tax_duplicate");
                }

                var versions = await db.TaxRateVersions
                    .Where(item => item.TaxId == command.TaxId)
                    .OrderBy(item => item.VersionNumber)
                    .ToListAsync(cancellationToken);
                if (versions.Any(item => item.EffectiveTo is not null
                    && Overlaps(item.EffectiveFrom, item.EffectiveTo, command.RateVersion.EffectiveFrom, command.RateVersion.EffectiveTo)))
                {
                    return MasterDataPersistenceResult<MasterDataTaxRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "tax_effective_overlap");
                }

                var openVersion = versions.SingleOrDefault(item => item.EffectiveTo is null);
                if (openVersion is not null)
                {
                    if (command.RateVersion.EffectiveFrom <= openVersion.EffectiveFrom)
                    {
                        return MasterDataPersistenceResult<MasterDataTaxRecord>.Denied(
                            MasterDataPersistenceOutcome.Conflict,
                            "tax_effective_overlap");
                    }

                    openVersion.CloseAt(command.RateVersion.EffectiveFrom.AddDays(-1));
                }

                var versionNumber = versions.Count == 0
                    ? 1
                    : versions.Max(item => item.VersionNumber) + 1;
                var version = CreateVersionEntity(
                    tenantContext.TenantId,
                    command.TaxId,
                    versionNumber,
                    command.RateVersion);
                entity.EditIdentity(
                    command.Code,
                    command.CategoryCode,
                    command.CategoryName,
                    command.Name,
                    command.Applicability,
                    versionNumber);
                db.TaxRateVersions.Add(version);
                return MasterDataPersistenceResult<MasterDataTaxRecord>.Success(
                    ToTaxRecord(entity, versions.Append(version)));
            },
            cancellationToken);
    }

    public Task<MasterDataPersistenceResult<MasterDataTaxRecord>> SetTaxLifecycleAsync(
        TenantContext tenantContext,
        Guid taxId,
        MasterDataLifecycleState lifecycleState,
        byte[] expectedVersion,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        return CommitAsync(
            tenantContext,
            evidence,
            "tax_lifecycle_no_change",
            async db =>
            {
                var entity = await db.Taxes.SingleOrDefaultAsync(
                    item => item.Id == taxId,
                    cancellationToken);
                if (entity is null)
                {
                    return MasterDataPersistenceResult<MasterDataTaxRecord>.Denied(
                        MasterDataPersistenceOutcome.NotFound,
                        "tax_not_found");
                }

                if (!VersionMatches(entity.Version, expectedVersion))
                {
                    return MasterDataPersistenceResult<MasterDataTaxRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "concurrency_conflict");
                }

                if (entity.LifecycleState == lifecycleState)
                {
                    return MasterDataPersistenceResult<MasterDataTaxRecord>.Denied(
                        MasterDataPersistenceOutcome.Failure,
                        "tax_lifecycle_no_change");
                }

                var versions = await db.TaxRateVersions
                    .Where(item => item.TaxId == taxId)
                    .OrderBy(item => item.VersionNumber)
                    .ToListAsync(cancellationToken);
                entity.SetLifecycle(lifecycleState);
                return MasterDataPersistenceResult<MasterDataTaxRecord>.Success(
                    ToTaxRecord(entity, versions));
            },
            cancellationToken);
    }

    public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(
        TenantContext tenantContext,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        CommitAsync(
            tenantContext,
            evidence,
            "audit_duplicate",
            db => Task.FromResult(
                MasterDataPersistenceResult<MasterDataAuditRecord>.Success(
                    ToAuditRecord(new MasterDataAuditEventEntity(evidence)))),
            cancellationToken,
            addEffect: false);

    public async Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(
        TenantContext tenantContext,
        Guid? taxId = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var query = db.AuditEvents
            .AsNoTracking()
            .Where(item => item.ResourceKind == MasterDataResourceKind.Tax);
        if (taxId is { } id)
        {
            query = query.Where(item => item.ResourceId == id);
        }

        var events = await query.ToListAsync(cancellationToken);
        return events
            .OrderByDescending(item => item.OccurredAt)
            .Select(ToAuditRecord)
            .ToArray();
    }

    private async Task<MasterDataPersistenceResult<T>> CommitAsync<T>(
        TenantContext tenantContext,
        MasterDataAuditEvidence evidence,
        string databaseFailureCode,
        Func<MasterDataDbContext, Task<MasterDataPersistenceResult<T>>> operation,
        CancellationToken cancellationToken,
        bool addEffect = true)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(operation);

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
        try
        {
            db.AuditEvents.Add(new MasterDataAuditEventEntity(evidence));
            var result = await operation(db);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return result;
            }

            _ = addEffect;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return MasterDataPersistenceResult<T>.Denied(
                MasterDataPersistenceOutcome.Conflict,
                "concurrency_conflict");
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return MasterDataPersistenceResult<T>.Denied(
                MasterDataPersistenceOutcome.Duplicate,
                databaseFailureCode);
        }
    }

    private MasterDataDbContext CreateContext(TenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        return new MasterDataDbContext(options, tenantContext);
    }

    private static MasterDataTaxRecord ToTaxRecord(MasterDataTaxEntity entity) =>
        ToTaxRecord(entity, entity.RateVersions.OrderBy(item => item.VersionNumber));

    private static MasterDataTaxRecord ToTaxRecord(
        MasterDataTaxEntity entity,
        IEnumerable<MasterDataTaxRateVersionEntity> versions) => new(
        entity.Id,
        entity.TenantId,
        entity.Code,
        entity.CategoryCode,
        entity.CategoryName,
        entity.Name,
        entity.Applicability,
        entity.LifecycleState,
        entity.CurrentVersionNumber,
        versions.OrderBy(item => item.VersionNumber).Select(ToRateVersionRecord).ToArray(),
        entity.Version.ToArray());

    private static MasterDataTaxRateVersionRecord ToRateVersionRecord(
        MasterDataTaxRateVersionEntity entity) => new(
        entity.Id,
        entity.VersionNumber,
        entity.EffectiveFrom,
        entity.EffectiveTo,
        entity.RatePercentage);

    private static MasterDataTaxRateVersionEntity CreateVersionEntity(
        TenantId tenantId,
        Guid taxId,
        int versionNumber,
        MasterDataTaxRateVersion version) => new(
        Guid.NewGuid(),
        tenantId,
        taxId,
        versionNumber,
        version.EffectiveFrom,
        version.EffectiveTo,
        version.RatePercentage);

    private static bool Overlaps(
        DateOnly leftFrom,
        DateOnly? leftTo,
        DateOnly rightFrom,
        DateOnly? rightTo) =>
        leftFrom <= (rightTo ?? DateOnly.MaxValue)
        && rightFrom <= (leftTo ?? DateOnly.MaxValue);

    private static bool VersionMatches(byte[] current, byte[] expected) =>
        expected is not null && current.AsSpan().SequenceEqual(expected);

    private static MasterDataAuditRecord ToAuditRecord(MasterDataAuditEventEntity entity)
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
