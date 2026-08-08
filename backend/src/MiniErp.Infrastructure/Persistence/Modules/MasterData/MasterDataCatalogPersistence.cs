#pragma warning disable CS1591

using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.Infrastructure.Persistence.Modules.MasterData;

/// <summary>
/// Tenant-bound persistence adapter for M95-SL-02. The adapter owns the
/// Master Data context, mappings, transaction boundary, and append-before-
/// effect audit write; it exposes only the application persistence contract.
/// </summary>
public sealed class MasterDataCatalogPersistence : IMasterDataCatalogPersistence
{
    private readonly DbContextOptions options;

    internal MasterDataCatalogPersistence(DbContextOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<IReadOnlyList<MasterDataCategoryRecord>> ListCategoriesAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        return await db.Categories
            .AsNoTracking()
            .OrderBy(item => item.Code)
            .Select(item => ToCategoryRecord(item))
            .ToListAsync(cancellationToken);
    }

    public async Task<MasterDataCategoryRecord?> FindCategoryAsync(
        TenantContext tenantContext,
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entity = await db.Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == categoryId, cancellationToken);
        return entity is null ? null : ToCategoryRecord(entity);
    }

    public Task<MasterDataPersistenceResult<MasterDataCategoryRecord>> CreateCategoryAsync(
        TenantContext tenantContext,
        Guid categoryId,
        CreateMasterDataCategoryCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CommitAsync(
            tenantContext,
            evidence,
            "duplicate_or_relationship_conflict",
            async db =>
            {
                if (await db.Categories.AnyAsync(item =>
                    item.Code == command.Code || item.NameKey == MasterDataCategoryUomValuePolicy.NameKey(command.Name),
                    cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataCategoryRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "category_duplicate");
                }

                if (command.ParentCategoryId is { } parentId
                    && !await db.Categories.AnyAsync(item => item.Id == parentId, cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataCategoryRecord>.Denied(
                        MasterDataPersistenceOutcome.InvalidReference,
                        "parent_category_not_found");
                }

                var entity = new MasterDataCategoryEntity(
                    categoryId,
                    tenantContext.TenantId,
                    command.Code,
                    command.Name,
                    command.ParentCategoryId);
                db.Categories.Add(entity);
                return MasterDataPersistenceResult<MasterDataCategoryRecord>.Success(ToCategoryRecord(entity));
            },
            cancellationToken);
    }

    public Task<MasterDataPersistenceResult<MasterDataCategoryRecord>> EditCategoryAsync(
        TenantContext tenantContext,
        EditMasterDataCategoryCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CommitAsync(
            tenantContext,
            evidence,
            "duplicate_or_relationship_conflict",
            async db =>
            {
                var entity = await db.Categories.SingleOrDefaultAsync(
                    item => item.Id == command.CategoryId,
                    cancellationToken);
                if (entity is null)
                {
                    return MasterDataPersistenceResult<MasterDataCategoryRecord>.Denied(
                        MasterDataPersistenceOutcome.NotFound,
                        "category_not_found");
                }

                if (!VersionMatches(entity.Version, command.ExpectedVersion))
                {
                    return MasterDataPersistenceResult<MasterDataCategoryRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "concurrency_conflict");
                }

                var nameKey = MasterDataCategoryUomValuePolicy.NameKey(command.Name);
                if (await db.Categories.AnyAsync(item =>
                    item.Id != command.CategoryId
                    && (item.Code == command.Code || item.NameKey == nameKey),
                    cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataCategoryRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "category_duplicate");
                }

                if (command.ParentCategoryId is { } parentId
                    && !await db.Categories.AnyAsync(item => item.Id == parentId, cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataCategoryRecord>.Denied(
                        MasterDataPersistenceOutcome.InvalidReference,
                        "parent_category_not_found");
                }

                entity.Edit(command.Code, command.Name, command.ParentCategoryId);
                entity.TouchVersion();
                return MasterDataPersistenceResult<MasterDataCategoryRecord>.Success(ToCategoryRecord(entity));
            },
            cancellationToken);
    }

    public Task<MasterDataPersistenceResult<MasterDataCategoryRecord>> SetCategoryLifecycleAsync(
        TenantContext tenantContext,
        Guid categoryId,
        MasterDataLifecycleState lifecycleState,
        byte[] expectedVersion,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        return CommitAsync(
            tenantContext,
            evidence,
            "concurrency_conflict",
            async db =>
            {
                var entity = await db.Categories.SingleOrDefaultAsync(
                    item => item.Id == categoryId,
                    cancellationToken);
                if (entity is null)
                {
                    return MasterDataPersistenceResult<MasterDataCategoryRecord>.Denied(
                        MasterDataPersistenceOutcome.NotFound,
                        "category_not_found");
                }

                if (!VersionMatches(entity.Version, expectedVersion))
                {
                    return MasterDataPersistenceResult<MasterDataCategoryRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "concurrency_conflict");
                }

                entity.SetLifecycle(lifecycleState);
                entity.TouchVersion();
                return MasterDataPersistenceResult<MasterDataCategoryRecord>.Success(ToCategoryRecord(entity));
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<MasterDataUnitOfMeasureRecord>> ListUnitsOfMeasureAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        return await db.UnitsOfMeasure
            .AsNoTracking()
            .OrderBy(item => item.Code)
            .Select(item => ToUnitRecord(item))
            .ToListAsync(cancellationToken);
    }

    public async Task<MasterDataUnitOfMeasureRecord?> FindUnitOfMeasureAsync(
        TenantContext tenantContext,
        Guid unitOfMeasureId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entity = await db.UnitsOfMeasure
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == unitOfMeasureId, cancellationToken);
        return entity is null ? null : ToUnitRecord(entity);
    }

    public Task<MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>> CreateUnitOfMeasureAsync(
        TenantContext tenantContext,
        Guid unitOfMeasureId,
        CreateMasterDataUnitOfMeasureCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CommitAsync(
            tenantContext,
            evidence,
            "duplicate_or_relationship_conflict",
            async db =>
            {
                var nameKey = MasterDataCategoryUomValuePolicy.NameKey(command.Name);
                if (await db.UnitsOfMeasure.AnyAsync(item =>
                    item.Code == command.Code || item.NameKey == nameKey,
                    cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "uom_duplicate");
                }

                var entity = new MasterDataUnitOfMeasureEntity(
                    unitOfMeasureId,
                    tenantContext.TenantId,
                    command.Code,
                    command.Name);
                db.UnitsOfMeasure.Add(entity);
                return MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>.Success(ToUnitRecord(entity));
            },
            cancellationToken);
    }

    public Task<MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>> EditUnitOfMeasureAsync(
        TenantContext tenantContext,
        EditMasterDataUnitOfMeasureCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CommitAsync(
            tenantContext,
            evidence,
            "duplicate_or_relationship_conflict",
            async db =>
            {
                var entity = await db.UnitsOfMeasure.SingleOrDefaultAsync(
                    item => item.Id == command.UnitOfMeasureId,
                    cancellationToken);
                if (entity is null)
                {
                    return MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>.Denied(
                        MasterDataPersistenceOutcome.NotFound,
                        "uom_not_found");
                }

                if (!VersionMatches(entity.Version, command.ExpectedVersion))
                {
                    return MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "concurrency_conflict");
                }

                var nameKey = MasterDataCategoryUomValuePolicy.NameKey(command.Name);
                if (await db.UnitsOfMeasure.AnyAsync(item =>
                    item.Id != command.UnitOfMeasureId
                    && (item.Code == command.Code || item.NameKey == nameKey),
                    cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "uom_duplicate");
                }

                entity.Edit(command.Code, command.Name);
                entity.TouchVersion();
                return MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>.Success(ToUnitRecord(entity));
            },
            cancellationToken);
    }

    public Task<MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>> SetUnitOfMeasureLifecycleAsync(
        TenantContext tenantContext,
        Guid unitOfMeasureId,
        MasterDataLifecycleState lifecycleState,
        byte[] expectedVersion,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        return CommitAsync(
            tenantContext,
            evidence,
            "concurrency_conflict",
            async db =>
            {
                var entity = await db.UnitsOfMeasure.SingleOrDefaultAsync(
                    item => item.Id == unitOfMeasureId,
                    cancellationToken);
                if (entity is null)
                {
                    return MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>.Denied(
                        MasterDataPersistenceOutcome.NotFound,
                        "uom_not_found");
                }

                if (!VersionMatches(entity.Version, expectedVersion))
                {
                    return MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "concurrency_conflict");
                }

                if (lifecycleState == MasterDataLifecycleState.Inactive
                    && await db.Conversions.AnyAsync(item =>
                        item.FromUnitOfMeasureId == unitOfMeasureId
                        || item.ToUnitOfMeasureId == unitOfMeasureId,
                        cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>.Denied(
                        MasterDataPersistenceOutcome.InUse,
                        "uom_in_use");
                }

                entity.SetLifecycle(lifecycleState);
                entity.TouchVersion();
                return MasterDataPersistenceResult<MasterDataUnitOfMeasureRecord>.Success(ToUnitRecord(entity));
            },
            cancellationToken);
    }

    public Task<MasterDataPersistenceResult<MasterDataConversionRecord>> CreateConversionAsync(
        TenantContext tenantContext,
        Guid conversionId,
        CreateMasterDataConversionCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CommitAsync(
            tenantContext,
            evidence,
            "duplicate_or_relationship_conflict",
            async db =>
            {
                if (await db.UnitsOfMeasure.AnyAsync(item =>
                        item.Id == command.FromUnitOfMeasureId
                        && item.LifecycleState != MasterDataLifecycleState.Active,
                    cancellationToken)
                    || await db.UnitsOfMeasure.AnyAsync(item =>
                        item.Id == command.ToUnitOfMeasureId
                        && item.LifecycleState != MasterDataLifecycleState.Active,
                    cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataConversionRecord>.Denied(
                        MasterDataPersistenceOutcome.InvalidReference,
                        "conversion_reference_inactive");
                }

                if (!await db.UnitsOfMeasure.AnyAsync(item => item.Id == command.FromUnitOfMeasureId, cancellationToken)
                    || !await db.UnitsOfMeasure.AnyAsync(item => item.Id == command.ToUnitOfMeasureId, cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataConversionRecord>.Denied(
                        MasterDataPersistenceOutcome.InvalidReference,
                        "conversion_reference_invalid");
                }

                if (await db.Conversions.AnyAsync(item =>
                    item.FromUnitOfMeasureId == command.FromUnitOfMeasureId
                    && item.ToUnitOfMeasureId == command.ToUnitOfMeasureId,
                    cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataConversionRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "conversion_duplicate");
                }

                var entity = new MasterDataConversionEntity(
                    conversionId,
                    tenantContext.TenantId,
                    command.FromUnitOfMeasureId,
                    command.ToUnitOfMeasureId,
                    command.Factor);
                db.Conversions.Add(entity);
                return MasterDataPersistenceResult<MasterDataConversionRecord>.Success(ToConversionRecord(entity));
            },
            cancellationToken);
    }

    public async Task<MasterDataQuantityConversionResult> ConvertQuantityAsync(
        TenantContext tenantContext,
        Guid conversionId,
        decimal quantity,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var conversion = await db.Conversions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == conversionId, cancellationToken);
        if (conversion is null)
        {
            return new MasterDataQuantityConversionResult(false, "conversion_not_found", null);
        }

        var source = await db.UnitsOfMeasure
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == conversion.FromUnitOfMeasureId, cancellationToken);
        var target = await db.UnitsOfMeasure
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == conversion.ToUnitOfMeasureId, cancellationToken);
        if (source is null || target is null)
        {
            return new MasterDataQuantityConversionResult(false, "conversion_reference_invalid", null);
        }

        if (source.LifecycleState != MasterDataLifecycleState.Active
            || target.LifecycleState != MasterDataLifecycleState.Active)
        {
            return new MasterDataQuantityConversionResult(false, "conversion_reference_inactive", null);
        }

        try
        {
            return new MasterDataQuantityConversionResult(
                true,
                "calculated",
                MasterDataCategoryUomValuePolicy.Calculate(quantity, conversion.Factor));
        }
        catch (ArgumentException)
        {
            return new MasterDataQuantityConversionResult(false, "precision_invalid", null);
        }
    }

    public async Task<bool> HasActiveConversionReferenceAsync(
        TenantContext tenantContext,
        Guid unitOfMeasureId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        return await db.Conversions.AnyAsync(item =>
            item.FromUnitOfMeasureId == unitOfMeasureId
            || item.ToUnitOfMeasureId == unitOfMeasureId,
            cancellationToken);
    }

    public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(
        TenantContext tenantContext,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        return CommitAsync(
            tenantContext,
            evidence,
            "audit_duplicate",
            db => Task.FromResult(
                MasterDataPersistenceResult<MasterDataAuditRecord>.Success(
                    ToAuditRecord(new MasterDataAuditEventEntity(evidence)))),
            cancellationToken,
            addEffect: false);
    }

    public async Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(
        TenantContext tenantContext,
        MasterDataResourceKind resourceKind,
        Guid? resourceId = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var query = db.AuditEvents
            .AsNoTracking()
            .Where(item => item.ResourceKind == resourceKind);
        if (resourceId is { } id)
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
            var auditEntity = new MasterDataAuditEventEntity(evidence);
            db.AuditEvents.Add(auditEntity);
            var result = await operation(db);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return result;
            }

            if (!addEffect)
            {
                // The audit entity is the effect for AppendAuditAsync. The
                // parameter exists only so the transaction helper remains
                // explicit at its call site.
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result with { Value = RefreshVersion(result.Value, db) };
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

    private static bool VersionMatches(byte[] current, byte[] expected) =>
        expected is not null && current.AsSpan().SequenceEqual(expected);

    private static T? RefreshVersion<T>(T? value, MasterDataDbContext db)
    {
        if (value is MasterDataCategoryRecord category)
        {
            var entity = db.Categories.Local.SingleOrDefault(candidate => candidate.Id == category.Id);
            return entity is null ? value : (T)(object)ToCategoryRecord(entity);
        }

        if (value is MasterDataUnitOfMeasureRecord unit)
        {
            var entity = db.UnitsOfMeasure.Local.SingleOrDefault(candidate => candidate.Id == unit.Id);
            return entity is null ? value : (T)(object)ToUnitRecord(entity);
        }

        if (value is MasterDataConversionRecord conversion)
        {
            var entity = db.Conversions.Local.SingleOrDefault(candidate => candidate.Id == conversion.Id);
            return entity is null ? value : (T)(object)ToConversionRecord(entity);
        }

        return value;
    }

    private static MasterDataCategoryRecord ToCategoryRecord(MasterDataCategoryEntity entity) => new(
        entity.Id,
        entity.TenantId,
        entity.Code,
        entity.Name,
        entity.ParentCategoryId,
        entity.LifecycleState,
        entity.Version.ToArray());

    private static MasterDataUnitOfMeasureRecord ToUnitRecord(MasterDataUnitOfMeasureEntity entity) => new(
        entity.Id,
        entity.TenantId,
        entity.Code,
        entity.Name,
        entity.LifecycleState,
        entity.Version.ToArray());

    private static MasterDataConversionRecord ToConversionRecord(MasterDataConversionEntity entity) => new(
        entity.Id,
        entity.TenantId,
        entity.FromUnitOfMeasureId,
        entity.ToUnitOfMeasureId,
        entity.Factor,
        entity.Version.ToArray());

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
