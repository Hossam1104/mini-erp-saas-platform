#pragma warning disable CS1591

using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.Infrastructure.Persistence.Modules.MasterData;

/// <summary>
/// Tenant-bound persistence for the MESP-120 Exchange Rate aggregate. The
/// Currency pair is an immutable identity; corrections append version rows and
/// retain the code snapshots used by historical reference evidence.
/// </summary>
public sealed class MasterDataExchangeRatePersistence : IMasterDataExchangeRatePersistence
{
    private readonly DbContextOptions options;

    internal MasterDataExchangeRatePersistence(DbContextOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<IReadOnlyList<MasterDataExchangeRateRecord>> ListExchangeRatesAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entities = await db.ExchangeRates
            .AsNoTracking()
            .Include(item => item.Versions)
            .OrderBy(item => item.SourceCurrencyId)
            .ThenBy(item => item.TargetCurrencyId)
            .ToListAsync(cancellationToken);
        var currencyCodes = await CurrencyCodesAsync(db, entities.SelectMany(item => new[] { item.SourceCurrencyId, item.TargetCurrencyId }), cancellationToken);
        return entities.Select(item => ToRecord(item, item.Versions, currencyCodes[item.SourceCurrencyId], currencyCodes[item.TargetCurrencyId])).ToArray();
    }

    public async Task<MasterDataExchangeRateRecord?> FindExchangeRateAsync(
        TenantContext tenantContext,
        Guid exchangeRateId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entity = await db.ExchangeRates
            .AsNoTracking()
            .Include(item => item.Versions)
            .SingleOrDefaultAsync(item => item.Id == exchangeRateId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var currencyCodes = await CurrencyCodesAsync(db, new[] { entity.SourceCurrencyId, entity.TargetCurrencyId }, cancellationToken);
        return ToRecord(entity, entity.Versions, currencyCodes[entity.SourceCurrencyId], currencyCodes[entity.TargetCurrencyId]);
    }

    public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> CreateExchangeRateAsync(
        TenantContext tenantContext,
        Guid exchangeRateId,
        CreateMasterDataExchangeRateCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CommitAsync(
            tenantContext,
            evidence,
            "exchange_rate_duplicate",
            async db =>
            {
                var currencies = await ActiveCurrenciesAsync(db, tenantContext.TenantId, command.SourceCurrencyId, command.TargetCurrencyId, cancellationToken);
                if (currencies.Count != 2)
                {
                    return MasterDataPersistenceResult<MasterDataExchangeRateRecord>.Denied(
                        MasterDataPersistenceOutcome.InvalidReference,
                        "exchange_rate_currency_not_found");
                }

                if (await db.ExchangeRates.AnyAsync(item =>
                    item.SourceCurrencyId == command.SourceCurrencyId
                    && item.TargetCurrencyId == command.TargetCurrencyId,
                    cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataExchangeRateRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "exchange_rate_duplicate");
                }

                var entity = new MasterDataExchangeRateEntity(
                    exchangeRateId,
                    tenantContext.TenantId,
                    command.SourceCurrencyId,
                    command.TargetCurrencyId);
                var version = CreateVersionEntity(
                    tenantContext.TenantId,
                    exchangeRateId,
                    1,
                    command,
                    currencies[command.SourceCurrencyId].Code,
                    currencies[command.TargetCurrencyId].Code);
                entity.Versions.Add(version);
                db.ExchangeRates.Add(entity);
                db.ExchangeRateVersions.Add(version);
                return MasterDataPersistenceResult<MasterDataExchangeRateRecord>.Success(
                    ToRecord(entity, [version], currencies[command.SourceCurrencyId].Code, currencies[command.TargetCurrencyId].Code));
            },
            cancellationToken);
    }

    public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> EditExchangeRateAsync(
        TenantContext tenantContext,
        EditMasterDataExchangeRateCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CommitAsync(
            tenantContext,
            evidence,
            "exchange_rate_duplicate",
            async db =>
            {
                var entity = await db.ExchangeRates.SingleOrDefaultAsync(item => item.Id == command.ExchangeRateId, cancellationToken);
                if (entity is null)
                {
                    return MasterDataPersistenceResult<MasterDataExchangeRateRecord>.Denied(
                        MasterDataPersistenceOutcome.NotFound,
                        "exchange_rate_not_found");
                }

                if (!VersionMatches(entity.Version, command.ExpectedVersion))
                {
                    return MasterDataPersistenceResult<MasterDataExchangeRateRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "concurrency_conflict");
                }

                if (entity.SourceCurrencyId != command.SourceCurrencyId || entity.TargetCurrencyId != command.TargetCurrencyId)
                {
                    return MasterDataPersistenceResult<MasterDataExchangeRateRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "exchange_rate_pair_immutable");
                }

                var currencies = await ActiveCurrenciesAsync(db, tenantContext.TenantId, entity.SourceCurrencyId, entity.TargetCurrencyId, cancellationToken);
                if (currencies.Count != 2)
                {
                    return MasterDataPersistenceResult<MasterDataExchangeRateRecord>.Denied(
                        MasterDataPersistenceOutcome.InvalidReference,
                        "exchange_rate_currency_not_found");
                }

                var versions = await db.ExchangeRateVersions
                    .Where(item => item.ExchangeRateId == command.ExchangeRateId)
                    .OrderBy(item => item.VersionNumber)
                    .ToListAsync(cancellationToken);
                if (versions.Any(item => item.EffectiveTo is not null
                    && Overlaps(item.EffectiveFrom, item.EffectiveTo, command.EffectiveFrom, command.EffectiveTo)))
                {
                    return MasterDataPersistenceResult<MasterDataExchangeRateRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "exchange_rate_effective_overlap");
                }

                var openVersion = versions.SingleOrDefault(item => item.EffectiveTo is null);
                if (openVersion is not null)
                {
                    if (command.EffectiveFrom <= openVersion.EffectiveFrom)
                    {
                        return MasterDataPersistenceResult<MasterDataExchangeRateRecord>.Denied(
                            MasterDataPersistenceOutcome.Conflict,
                            "exchange_rate_effective_overlap");
                    }

                    openVersion.CloseAt(command.EffectiveFrom.AddDays(-1));
                }

                var versionNumber = versions.Count == 0 ? 1 : versions.Max(item => item.VersionNumber) + 1;
                var version = CreateVersionEntity(
                    tenantContext.TenantId,
                    command.ExchangeRateId,
                    versionNumber,
                    command,
                    currencies[entity.SourceCurrencyId].Code,
                    currencies[entity.TargetCurrencyId].Code);
                entity.AppendVersion(versionNumber);
                entity.Versions.Add(version);
                db.ExchangeRateVersions.Add(version);
                return MasterDataPersistenceResult<MasterDataExchangeRateRecord>.Success(
                    ToRecord(entity, versions.Append(version), currencies[entity.SourceCurrencyId].Code, currencies[entity.TargetCurrencyId].Code));
            },
            cancellationToken);
    }

    public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> SetExchangeRateLifecycleAsync(
        TenantContext tenantContext,
        Guid exchangeRateId,
        MasterDataLifecycleState lifecycleState,
        byte[] expectedVersion,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        CommitAsync(
            tenantContext,
            evidence,
            "exchange_rate_lifecycle_no_change",
            async db =>
            {
                var entity = await db.ExchangeRates
                    .Include(item => item.Versions)
                    .SingleOrDefaultAsync(item => item.Id == exchangeRateId, cancellationToken);
                if (entity is null)
                {
                    return MasterDataPersistenceResult<MasterDataExchangeRateRecord>.Denied(
                        MasterDataPersistenceOutcome.NotFound,
                        "exchange_rate_not_found");
                }

                if (!VersionMatches(entity.Version, expectedVersion))
                {
                    return MasterDataPersistenceResult<MasterDataExchangeRateRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "concurrency_conflict");
                }

                if (entity.LifecycleState == lifecycleState)
                {
                    return MasterDataPersistenceResult<MasterDataExchangeRateRecord>.Denied(
                        MasterDataPersistenceOutcome.Failure,
                        "exchange_rate_lifecycle_no_change");
                }

                var currencyCodes = await CurrencyCodesAsync(db, new[] { entity.SourceCurrencyId, entity.TargetCurrencyId }, cancellationToken);
                entity.SetLifecycle(lifecycleState);
                return MasterDataPersistenceResult<MasterDataExchangeRateRecord>.Success(
                    ToRecord(entity, entity.Versions, currencyCodes[entity.SourceCurrencyId], currencyCodes[entity.TargetCurrencyId]));
            },
            cancellationToken);

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
        Guid? exchangeRateId = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var query = db.AuditEvents
            .AsNoTracking()
            .Where(item => item.ResourceKind == MasterDataResourceKind.ExchangeRate);
        if (exchangeRateId is { } id)
        {
            query = query.Where(item => item.ResourceId == id);
        }

        var events = await query.ToListAsync(cancellationToken);
        return events.OrderByDescending(item => item.OccurredAt).Select(ToAuditRecord).ToArray();
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
            return MasterDataPersistenceResult<T>.Denied(MasterDataPersistenceOutcome.AuditFailure, "audit_context_mismatch");
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
            return MasterDataPersistenceResult<T>.Denied(MasterDataPersistenceOutcome.Conflict, "concurrency_conflict");
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return MasterDataPersistenceResult<T>.Denied(MasterDataPersistenceOutcome.Duplicate, databaseFailureCode);
        }
    }

    private MasterDataDbContext CreateContext(TenantContext tenantContext) => new(options, tenantContext);

    private static async Task<Dictionary<Guid, MasterDataCurrencyEntity>> ActiveCurrenciesAsync(
        MasterDataDbContext db,
        TenantId tenantId,
        Guid sourceCurrencyId,
        Guid targetCurrencyId,
        CancellationToken cancellationToken)
    {
        var ids = new[] { sourceCurrencyId, targetCurrencyId };
        return await db.Currencies
            .Where(item => ids.Contains(item.Id) && item.LifecycleState == MasterDataLifecycleState.Active)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
    }

    private static async Task<Dictionary<Guid, string>> CurrencyCodesAsync(
        MasterDataDbContext db,
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken)
    {
        var uniqueIds = ids.Distinct().ToArray();
        return await db.Currencies
            .Where(item => uniqueIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Code, cancellationToken);
    }

    private static MasterDataExchangeRateVersionEntity CreateVersionEntity(
        TenantId tenantId,
        Guid exchangeRateId,
        int versionNumber,
        CreateMasterDataExchangeRateCommand command,
        string sourceCurrencyCode,
        string targetCurrencyCode) =>
        new(
            Guid.NewGuid(),
            tenantId,
            exchangeRateId,
            versionNumber,
            command.EffectiveFrom,
            command.EffectiveTo,
            command.Rate,
            command.RateScale,
            command.Provenance,
            command.SourceNotes,
            sourceCurrencyCode,
            targetCurrencyCode);

    private static MasterDataExchangeRateVersionEntity CreateVersionEntity(
        TenantId tenantId,
        Guid exchangeRateId,
        int versionNumber,
        EditMasterDataExchangeRateCommand command,
        string sourceCurrencyCode,
        string targetCurrencyCode) =>
        new(
            Guid.NewGuid(),
            tenantId,
            exchangeRateId,
            versionNumber,
            command.EffectiveFrom,
            command.EffectiveTo,
            command.Rate,
            command.RateScale,
            command.Provenance,
            command.SourceNotes,
            sourceCurrencyCode,
            targetCurrencyCode);

    private static MasterDataExchangeRateRecord ToRecord(
        MasterDataExchangeRateEntity entity,
        IEnumerable<MasterDataExchangeRateVersionEntity> versions,
        string sourceCurrencyCode,
        string targetCurrencyCode) =>
        new(
            entity.Id,
            entity.TenantId,
            entity.SourceCurrencyId,
            entity.TargetCurrencyId,
            sourceCurrencyCode,
            targetCurrencyCode,
            entity.LifecycleState,
            entity.CurrentVersionNumber,
            versions.OrderBy(item => item.VersionNumber).Select(ToVersionRecord).ToArray(),
            entity.Version.ToArray());

    private static MasterDataExchangeRateVersionRecord ToVersionRecord(MasterDataExchangeRateVersionEntity entity) =>
        new(
            entity.Id,
            entity.VersionNumber,
            entity.EffectiveFrom,
            entity.EffectiveTo,
            entity.Rate,
            entity.RateScale,
            entity.Provenance,
            entity.SourceNotes,
            entity.SourceCurrencyCode,
            entity.TargetCurrencyCode);

    private static bool Overlaps(DateOnly leftFrom, DateOnly? leftTo, DateOnly rightFrom, DateOnly? rightTo) =>
        leftFrom <= (rightTo ?? DateOnly.MaxValue) && rightFrom <= (leftTo ?? DateOnly.MaxValue);

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

            scope = new BusinessScope(tenant, anchor, new ScopePolicyReference(entity.ScopePolicyId, entity.ScopePolicyVersion));
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
