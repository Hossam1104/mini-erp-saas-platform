#pragma warning disable CS1591

using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.Infrastructure.Persistence.Modules.MasterData;

/// <summary>
/// Tenant-bound persistence for the MESP-118 Currency and Payment Terms
/// configuration contract. Currency identity and Payment Term version rows are
/// committed with their audit evidence in one module-owned transaction.
/// </summary>
public sealed class MasterDataCurrencyPaymentTermPersistence : IMasterDataCurrencyPaymentTermPersistence
{
    private readonly DbContextOptions options;

    internal MasterDataCurrencyPaymentTermPersistence(DbContextOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<IReadOnlyList<MasterDataCurrencyRecord>> ListCurrenciesAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        return (await db.Currencies
                .AsNoTracking()
                .OrderBy(item => item.Code)
                .ToListAsync(cancellationToken))
            .Select(ToCurrencyRecord)
            .ToArray();
    }

    public async Task<MasterDataCurrencyRecord?> FindCurrencyAsync(
        TenantContext tenantContext,
        Guid currencyId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entity = await db.Currencies
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == currencyId, cancellationToken);
        return entity is null ? null : ToCurrencyRecord(entity);
    }

    public Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>> CreateCurrencyAsync(
        TenantContext tenantContext,
        Guid currencyId,
        CreateMasterDataCurrencyCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CommitAsync(
            tenantContext,
            evidence,
            "currency_duplicate",
            async db =>
            {
                var nameKey = MasterDataCurrencyPaymentTermValuePolicy.NameKey(command.Name);
                if (await db.Currencies.AnyAsync(
                    item => item.CodeKey == command.Code || item.NameKey == nameKey,
                    cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataCurrencyRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "currency_duplicate");
                }

                var entity = new MasterDataCurrencyEntity(
                    currencyId,
                    tenantContext.TenantId,
                    command.Code,
                    command.Name);
                db.Currencies.Add(entity);
                return MasterDataPersistenceResult<MasterDataCurrencyRecord>.Success(ToCurrencyRecord(entity));
            },
            cancellationToken);
    }

    public Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>> EditCurrencyAsync(
        TenantContext tenantContext,
        EditMasterDataCurrencyCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CommitAsync(
            tenantContext,
            evidence,
            "currency_duplicate",
            async db =>
            {
                var entity = await db.Currencies.SingleOrDefaultAsync(
                    item => item.Id == command.CurrencyId,
                    cancellationToken);
                if (entity is null)
                {
                    return MasterDataPersistenceResult<MasterDataCurrencyRecord>.Denied(
                        MasterDataPersistenceOutcome.NotFound,
                        "currency_not_found");
                }

                if (!VersionMatches(entity.Version, command.ExpectedVersion))
                {
                    return MasterDataPersistenceResult<MasterDataCurrencyRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "concurrency_conflict");
                }

                var nameKey = MasterDataCurrencyPaymentTermValuePolicy.NameKey(command.Name);
                if (await db.Currencies.AnyAsync(
                    item => item.Id != command.CurrencyId
                        && (item.CodeKey == command.Code || item.NameKey == nameKey),
                    cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataCurrencyRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "currency_duplicate");
                }

                entity.Edit(command.Code, command.Name);
                return MasterDataPersistenceResult<MasterDataCurrencyRecord>.Success(ToCurrencyRecord(entity));
            },
            cancellationToken);
    }

    public Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>> SetCurrencyLifecycleAsync(
        TenantContext tenantContext,
        Guid currencyId,
        MasterDataLifecycleState lifecycleState,
        byte[] expectedVersion,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        return CommitAsync(
            tenantContext,
            evidence,
            "currency_lifecycle_no_change",
            async db =>
            {
                var entity = await db.Currencies.SingleOrDefaultAsync(
                    item => item.Id == currencyId,
                    cancellationToken);
                if (entity is null)
                {
                    return MasterDataPersistenceResult<MasterDataCurrencyRecord>.Denied(
                        MasterDataPersistenceOutcome.NotFound,
                        "currency_not_found");
                }

                if (!VersionMatches(entity.Version, expectedVersion))
                {
                    return MasterDataPersistenceResult<MasterDataCurrencyRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "concurrency_conflict");
                }

                if (entity.LifecycleState == lifecycleState)
                {
                    return MasterDataPersistenceResult<MasterDataCurrencyRecord>.Denied(
                        MasterDataPersistenceOutcome.Failure,
                        "currency_lifecycle_no_change");
                }

                entity.SetLifecycle(lifecycleState);
                return MasterDataPersistenceResult<MasterDataCurrencyRecord>.Success(ToCurrencyRecord(entity));
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<MasterDataPaymentTermRecord>> ListPaymentTermsAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entities = await db.PaymentTerms
            .AsNoTracking()
            .Include(item => item.Versions)
            .ThenInclude(item => item.Installments)
            .OrderBy(item => item.Code)
            .ToListAsync(cancellationToken);
        return entities.Select(ToPaymentTermRecord).ToArray();
    }

    public async Task<MasterDataPaymentTermRecord?> FindPaymentTermAsync(
        TenantContext tenantContext,
        Guid paymentTermId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entity = await db.PaymentTerms
            .AsNoTracking()
            .Include(item => item.Versions)
            .ThenInclude(item => item.Installments)
            .SingleOrDefaultAsync(item => item.Id == paymentTermId, cancellationToken);
        return entity is null ? null : ToPaymentTermRecord(entity);
    }

    public Task<MasterDataPersistenceResult<MasterDataPaymentTermRecord>> CreatePaymentTermAsync(
        TenantContext tenantContext,
        Guid paymentTermId,
        CreateMasterDataPaymentTermCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CommitAsync(
            tenantContext,
            evidence,
            "payment_term_duplicate",
            async db =>
            {
                var nameKey = MasterDataCurrencyPaymentTermValuePolicy.NameKey(command.Name);
                if (await db.PaymentTerms.AnyAsync(
                    item => item.CodeKey == command.Code || item.NameKey == nameKey,
                    cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataPaymentTermRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "payment_term_duplicate");
                }

                var entity = new MasterDataPaymentTermEntity(
                    paymentTermId,
                    tenantContext.TenantId,
                    command.Code,
                    command.Name);
                var version = CreateVersionEntity(
                    tenantContext.TenantId,
                    paymentTermId,
                    1,
                    command.Code,
                    command.Name,
                    command.EffectiveFrom,
                    command.EffectiveTo,
                    command.BaseDateRule,
                    command.ScheduleMode,
                    command.DueOffset,
                    command.Installments,
                    command.EarlySettlementDiscount);
                db.PaymentTerms.Add(entity);
                db.PaymentTermVersions.Add(version);
                foreach (var installment in version.Installments)
                {
                    db.PaymentTermInstallments.Add(installment);
                }

                return MasterDataPersistenceResult<MasterDataPaymentTermRecord>.Success(
                    ToPaymentTermRecord(entity, [version]));
            },
            cancellationToken);
    }

    public Task<MasterDataPersistenceResult<MasterDataPaymentTermRecord>> EditPaymentTermAsync(
        TenantContext tenantContext,
        EditMasterDataPaymentTermCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CommitAsync(
            tenantContext,
            evidence,
            "payment_term_duplicate",
            async db =>
            {
                var entity = await db.PaymentTerms.SingleOrDefaultAsync(
                    item => item.Id == command.PaymentTermId,
                    cancellationToken);
                if (entity is null)
                {
                    return MasterDataPersistenceResult<MasterDataPaymentTermRecord>.Denied(
                        MasterDataPersistenceOutcome.NotFound,
                        "payment_term_not_found");
                }

                if (!VersionMatches(entity.Version, command.ExpectedVersion))
                {
                    return MasterDataPersistenceResult<MasterDataPaymentTermRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "concurrency_conflict");
                }

                var nameKey = MasterDataCurrencyPaymentTermValuePolicy.NameKey(command.Name);
                if (await db.PaymentTerms.AnyAsync(
                    item => item.Id != command.PaymentTermId
                        && (item.CodeKey == command.Code || item.NameKey == nameKey),
                    cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataPaymentTermRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "payment_term_duplicate");
                }

                var versions = await db.PaymentTermVersions
                    .Where(item => item.PaymentTermId == command.PaymentTermId)
                    .Include(item => item.Installments)
                    .OrderBy(item => item.VersionNumber)
                    .ToListAsync(cancellationToken);
                if (versions.Any(item => Overlaps(
                    item.EffectiveFrom,
                    item.EffectiveTo,
                    command.EffectiveFrom,
                    command.EffectiveTo)
                    && item.EffectiveTo is not null))
                {
                    return MasterDataPersistenceResult<MasterDataPaymentTermRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "payment_term_effective_overlap");
                }

                var openVersion = versions.SingleOrDefault(item => item.EffectiveTo is null);
                if (openVersion is not null)
                {
                    if (command.EffectiveFrom <= openVersion.EffectiveFrom)
                    {
                        return MasterDataPersistenceResult<MasterDataPaymentTermRecord>.Denied(
                            MasterDataPersistenceOutcome.Conflict,
                            "payment_term_effective_overlap");
                    }

                    openVersion.CloseAt(command.EffectiveFrom.AddDays(-1));
                }

                var versionNumber = versions.Count == 0
                    ? 1
                    : versions.Max(item => item.VersionNumber) + 1;
                var version = CreateVersionEntity(
                    tenantContext.TenantId,
                    command.PaymentTermId,
                    versionNumber,
                    command.Code,
                    command.Name,
                    command.EffectiveFrom,
                    command.EffectiveTo,
                    command.BaseDateRule,
                    command.ScheduleMode,
                    command.DueOffset,
                    command.Installments,
                    command.EarlySettlementDiscount);
                entity.EditIdentity(command.Code, command.Name, versionNumber);
                db.PaymentTermVersions.Add(version);
                foreach (var installment in version.Installments)
                {
                    db.PaymentTermInstallments.Add(installment);
                }

                return MasterDataPersistenceResult<MasterDataPaymentTermRecord>.Success(
                    ToPaymentTermRecord(entity, versions.Append(version).ToArray()));
            },
            cancellationToken);
    }

    public Task<MasterDataPersistenceResult<MasterDataPaymentTermRecord>> SetPaymentTermLifecycleAsync(
        TenantContext tenantContext,
        Guid paymentTermId,
        MasterDataLifecycleState lifecycleState,
        byte[] expectedVersion,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        return CommitAsync(
            tenantContext,
            evidence,
            "payment_term_lifecycle_no_change",
            async db =>
            {
                var entity = await db.PaymentTerms.SingleOrDefaultAsync(
                    item => item.Id == paymentTermId,
                    cancellationToken);
                if (entity is null)
                {
                    return MasterDataPersistenceResult<MasterDataPaymentTermRecord>.Denied(
                        MasterDataPersistenceOutcome.NotFound,
                        "payment_term_not_found");
                }

                if (!VersionMatches(entity.Version, expectedVersion))
                {
                    return MasterDataPersistenceResult<MasterDataPaymentTermRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "concurrency_conflict");
                }

                if (entity.LifecycleState == lifecycleState)
                {
                    return MasterDataPersistenceResult<MasterDataPaymentTermRecord>.Denied(
                        MasterDataPersistenceOutcome.Failure,
                        "payment_term_lifecycle_no_change");
                }

                var versions = await db.PaymentTermVersions
                    .Where(item => item.PaymentTermId == paymentTermId)
                    .Include(item => item.Installments)
                    .OrderBy(item => item.VersionNumber)
                    .ToListAsync(cancellationToken);
                entity.SetLifecycle(lifecycleState);
                return MasterDataPersistenceResult<MasterDataPaymentTermRecord>.Success(
                    ToPaymentTermRecord(entity, versions));
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

    private static MasterDataCurrencyRecord ToCurrencyRecord(MasterDataCurrencyEntity entity) => new(
        entity.Id,
        entity.TenantId,
        entity.Code,
        entity.Name,
        entity.LifecycleState,
        entity.Revision,
        entity.Version.ToArray());

    private static MasterDataPaymentTermRecord ToPaymentTermRecord(
        MasterDataPaymentTermEntity entity) =>
        ToPaymentTermRecord(entity, entity.Versions.OrderBy(item => item.VersionNumber));

    private static MasterDataPaymentTermRecord ToPaymentTermRecord(
        MasterDataPaymentTermEntity entity,
        IEnumerable<MasterDataPaymentTermVersionEntity> versions) => new(
        entity.Id,
        entity.TenantId,
        entity.Code,
        entity.Name,
        entity.LifecycleState,
        entity.CurrentVersionNumber,
        versions
            .OrderBy(item => item.VersionNumber)
            .Select(ToPaymentTermVersionRecord)
            .ToArray(),
        entity.Version.ToArray());

    private static MasterDataPaymentTermVersionRecord ToPaymentTermVersionRecord(
        MasterDataPaymentTermVersionEntity entity) => new(
        entity.Id,
        entity.VersionNumber,
        entity.EffectiveFrom,
        entity.EffectiveTo,
        entity.BaseDateRule,
        entity.ScheduleMode,
        new MasterDataPaymentTermOffset(entity.DueOffsetDays, entity.DueOffsetMonths),
        entity.Installments
            .OrderBy(item => item.Sequence)
            .Select(item => new MasterDataPaymentTermInstallment(
                item.Sequence,
                item.Percentage,
                new MasterDataPaymentTermOffset(item.OffsetDays, item.OffsetMonths)))
            .ToArray(),
        new MasterDataEarlySettlementDiscount(
            entity.EarlySettlementDiscountEnabled,
            entity.EarlySettlementDiscountPercentage,
            new MasterDataPaymentTermOffset(
                entity.EarlySettlementDiscountDays,
                entity.EarlySettlementDiscountMonths)),
        entity.Code,
        entity.Name);

    private static MasterDataPaymentTermVersionEntity CreateVersionEntity(
        TenantId tenantId,
        Guid paymentTermId,
        int versionNumber,
        string code,
        LocalizedName name,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        PaymentTermBaseDateRule baseDateRule,
        PaymentTermScheduleMode scheduleMode,
        MasterDataPaymentTermOffset dueOffset,
        IReadOnlyList<MasterDataPaymentTermInstallment> installments,
        MasterDataEarlySettlementDiscount earlySettlementDiscount)
    {
        var version = new MasterDataPaymentTermVersionEntity(
            Guid.NewGuid(),
            tenantId,
            paymentTermId,
            versionNumber,
            code,
            name,
            effectiveFrom,
            effectiveTo,
            baseDateRule,
            scheduleMode,
            dueOffset.Days,
            dueOffset.Months,
            earlySettlementDiscount.Enabled,
            earlySettlementDiscount.Percentage,
            earlySettlementDiscount.Offset.Days,
            earlySettlementDiscount.Offset.Months);
        foreach (var installment in installments)
        {
            version.Installments.Add(new MasterDataPaymentTermInstallmentEntity(
                Guid.NewGuid(),
                tenantId,
                version.Id,
                installment.Sequence,
                installment.Percentage,
                installment.Offset.Days,
                installment.Offset.Months));
        }

        return version;
    }

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
