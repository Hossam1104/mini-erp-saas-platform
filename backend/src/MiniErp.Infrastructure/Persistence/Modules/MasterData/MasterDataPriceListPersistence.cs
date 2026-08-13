#pragma warning disable CS1591

using System.Data;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.Infrastructure.Persistence.Modules.MasterData;

/// <summary>
/// Tenant-bound Price List adapter. Product, UOM, and Currency references are
/// validated against the Master Data context; Customer applicability is read
/// through the Business Parties reference port. No cross-module EF join,
/// migration, database creation, or provider integration is performed here.
/// </summary>
public sealed class MasterDataPriceListPersistence : IMasterDataPriceListPersistence
{
    private readonly DbContextOptions options;
    private readonly IBusinessCustomerReferenceReader? customerReader;

    internal MasterDataPriceListPersistence(
        DbContextOptions options,
        IBusinessCustomerReferenceReader? customerReader = null)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.customerReader = customerReader;
    }

    public async Task<IReadOnlyList<MasterDataPriceListRecord>> ListPriceListsAsync(
        TenantContext tenantContext,
        string? search,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var query = db.PriceLists
            .AsNoTracking()
            .Include(item => item.Prices)
            .AsQueryable();
        var normalizedSearch = search?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var key = normalizedSearch.ToUpperInvariant();
            query = query.Where(item => item.CodeKey.Contains(key) || item.EnglishName.Contains(normalizedSearch));
        }

        var entities = await query
            .OrderBy(item => item.CodeKey)
            .ToListAsync(cancellationToken);
        return entities
            .Where(item => IsOrganizationVisible(tenantContext, item))
            .Select(ToRecord)
            .ToArray();
    }

    public async Task<MasterDataPriceListRecord?> FindPriceListAsync(
        TenantContext tenantContext,
        Guid priceListId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entity = await db.PriceLists
            .AsNoTracking()
            .Include(item => item.Prices)
            .SingleOrDefaultAsync(item => item.Id == priceListId, cancellationToken);
        return entity is null || !IsOrganizationVisible(tenantContext, entity)
            ? null
            : ToRecord(entity);
    }

    public Task<MasterDataPersistenceResult<MasterDataPriceListRecord>> CreatePriceListAsync(
        TenantContext tenantContext,
        Guid priceListId,
        CreateMasterDataPriceListCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CommitAsync(
            tenantContext,
            evidence,
            "price_list_duplicate",
            async db =>
            {
                if (!IsOrganizationRequestAllowed(tenantContext, command.OrganizationScopeKind, command.OrganizationScopeId))
                {
                    return MasterDataPersistenceResult<MasterDataPriceListRecord>.Denied(
                        MasterDataPersistenceOutcome.Failure,
                        "organization_scope_denied");
                }

                if (await db.PriceLists.AnyAsync(item => item.CodeKey == command.Code.ToUpperInvariant(), cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataPriceListRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "price_list_duplicate");
                }

                var currency = await db.Currencies
                    .SingleOrDefaultAsync(item => item.Id == command.CurrencyId && item.LifecycleState == MasterDataLifecycleState.Active, cancellationToken);
                if (currency is null)
                {
                    return MasterDataPersistenceResult<MasterDataPriceListRecord>.Denied(
                        MasterDataPersistenceOutcome.InvalidReference,
                        "price_list_currency_not_found");
                }

                var customerCode = await ValidateCustomerReferenceAsync(tenantContext, command.CustomerId, cancellationToken);
                if (customerCode is not null)
                {
                    return MasterDataPersistenceResult<MasterDataPriceListRecord>.Denied(
                        MasterDataPersistenceOutcome.InvalidReference,
                        customerCode);
                }

                var entity = new MasterDataPriceListEntity(
                    priceListId,
                    tenantContext.TenantId,
                    command.Code,
                    command.Name,
                    command.CurrencyId,
                    currency.Code,
                    command.CustomerId,
                    command.OrganizationScopeKind,
                    command.OrganizationScopeId,
                    command.Priority);
                db.PriceLists.Add(entity);
                return MasterDataPersistenceResult<MasterDataPriceListRecord>.Success(ToRecord(entity));
            },
            cancellationToken);
    }

    public Task<MasterDataPersistenceResult<MasterDataPriceListRecord>> EditPriceListAsync(
        TenantContext tenantContext,
        EditMasterDataPriceListCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CommitAsync(
            tenantContext,
            evidence,
            "price_list_duplicate",
            async db =>
            {
                var entity = await db.PriceLists
                    .Include(item => item.Prices)
                    .SingleOrDefaultAsync(item => item.Id == command.PriceListId, cancellationToken);
                if (entity is null)
                {
                    return MasterDataPersistenceResult<MasterDataPriceListRecord>.Denied(
                        MasterDataPersistenceOutcome.NotFound,
                        "price_list_not_found");
                }

                if (!IsOrganizationVisible(tenantContext, entity)
                    || !IsOrganizationRequestAllowed(tenantContext, command.OrganizationScopeKind, command.OrganizationScopeId))
                {
                    return MasterDataPersistenceResult<MasterDataPriceListRecord>.Denied(
                        MasterDataPersistenceOutcome.Failure,
                        "organization_scope_denied");
                }

                if (!VersionMatches(entity.Version, command.ExpectedVersion))
                {
                    return MasterDataPersistenceResult<MasterDataPriceListRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "concurrency_conflict");
                }

                if (await db.PriceLists.AnyAsync(item => item.Id != command.PriceListId && item.CodeKey == command.Code.ToUpperInvariant(), cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataPriceListRecord>.Denied(
                        MasterDataPersistenceOutcome.Duplicate,
                        "price_list_duplicate");
                }

                var currency = await db.Currencies
                    .SingleOrDefaultAsync(item => item.Id == command.CurrencyId && item.LifecycleState == MasterDataLifecycleState.Active, cancellationToken);
                if (currency is null)
                {
                    return MasterDataPersistenceResult<MasterDataPriceListRecord>.Denied(
                        MasterDataPersistenceOutcome.InvalidReference,
                        "price_list_currency_not_found");
                }

                var customerCode = await ValidateCustomerReferenceAsync(tenantContext, command.CustomerId, cancellationToken);
                if (customerCode is not null)
                {
                    return MasterDataPersistenceResult<MasterDataPriceListRecord>.Denied(
                        MasterDataPersistenceOutcome.InvalidReference,
                        customerCode);
                }

                if (entity.LifecycleState == MasterDataLifecycleState.Active
                    && await HasProposedEditPrecedenceConflictAsync(
                        db,
                        entity,
                        command,
                        cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataPriceListRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "price_list_precedence_conflict");
                }

                entity.Edit(
                    command.Code,
                    command.Name,
                    command.CurrencyId,
                    currency.Code,
                    command.CustomerId,
                    command.OrganizationScopeKind,
                    command.OrganizationScopeId,
                    command.Priority);
                return MasterDataPersistenceResult<MasterDataPriceListRecord>.Success(ToRecord(entity));
            },
            cancellationToken);
    }

    public Task<MasterDataPersistenceResult<MasterDataPriceListRecord>> AppendPriceAsync(
        TenantContext tenantContext,
        AppendMasterDataPriceCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CommitAsync(
            tenantContext,
            evidence,
            "price_list_duplicate",
            async db =>
            {
                var entity = await db.PriceLists
                    .Include(item => item.Prices)
                    .SingleOrDefaultAsync(item => item.Id == command.PriceListId, cancellationToken);
                if (entity is null)
                {
                    return MasterDataPersistenceResult<MasterDataPriceListRecord>.Denied(
                        MasterDataPersistenceOutcome.NotFound,
                        "price_list_not_found");
                }

                if (!IsOrganizationVisible(tenantContext, entity))
                {
                    return MasterDataPersistenceResult<MasterDataPriceListRecord>.Denied(
                        MasterDataPersistenceOutcome.Failure,
                        "organization_scope_denied");
                }

                if (!VersionMatches(entity.Version, command.ExpectedVersion))
                {
                    return MasterDataPersistenceResult<MasterDataPriceListRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "concurrency_conflict");
                }

                if (entity.LifecycleState != MasterDataLifecycleState.Active)
                {
                    return MasterDataPersistenceResult<MasterDataPriceListRecord>.Denied(
                        MasterDataPersistenceOutcome.Failure,
                        "price_list_inactive");
                }

                var product = await db.Products
                    .SingleOrDefaultAsync(item => item.Id == command.ProductId && item.LifecycleState == MasterDataLifecycleState.Active, cancellationToken);
                if (product is null)
                {
                    return MasterDataPersistenceResult<MasterDataPriceListRecord>.Denied(
                        MasterDataPersistenceOutcome.InvalidReference,
                        "price_list_product_not_found");
                }

                var unit = await db.UnitsOfMeasure
                    .SingleOrDefaultAsync(item => item.Id == command.UnitOfMeasureId && item.LifecycleState == MasterDataLifecycleState.Active, cancellationToken);
                if (unit is null)
                {
                    return MasterDataPersistenceResult<MasterDataPriceListRecord>.Denied(
                        MasterDataPersistenceOutcome.InvalidReference,
                        "price_list_uom_not_found");
                }

                var currency = await db.Currencies
                    .SingleOrDefaultAsync(item => item.Id == entity.CurrencyId && item.LifecycleState == MasterDataLifecycleState.Active, cancellationToken);
                if (currency is null)
                {
                    return MasterDataPersistenceResult<MasterDataPriceListRecord>.Denied(
                        MasterDataPersistenceOutcome.InvalidReference,
                        "price_list_currency_not_found");
                }

                if (entity.Prices.Any(item => item.ProductId == command.ProductId
                    && item.UnitOfMeasureId == command.UnitOfMeasureId
                    && Overlaps(item.EffectiveFrom, item.EffectiveTo, command.EffectiveFrom, command.EffectiveTo)))
                {
                    return MasterDataPersistenceResult<MasterDataPriceListRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "price_list_effective_overlap");
                }

                if (await HasCrossListPrecedenceConflictAsync(db, entity, command, cancellationToken))
                {
                    return MasterDataPersistenceResult<MasterDataPriceListRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "price_list_precedence_conflict");
                }

                var versionNumber = entity.CurrentVersionNumber + 1;
                var price = new MasterDataPriceListPriceEntity(
                    Guid.NewGuid(),
                    tenantContext.TenantId,
                    entity.Id,
                    versionNumber,
                    command.ProductId,
                    product.Sku,
                    command.UnitOfMeasureId,
                    unit.Code,
                    entity.CurrencyId,
                    currency.Code,
                    entity.CustomerId,
                    entity.OrganizationScopeKind,
                    entity.OrganizationScopeId,
                    entity.Priority,
                    command.EffectiveFrom,
                    command.EffectiveTo,
                    command.Price,
                    command.PriceScale,
                    command.Provenance,
                    command.SourceReference);
                entity.Prices.Add(price);
                entity.AppendVersion(versionNumber);
                db.PriceListPrices.Add(price);
                return MasterDataPersistenceResult<MasterDataPriceListRecord>.Success(ToRecord(entity));
            },
            cancellationToken);
    }

    public Task<MasterDataPersistenceResult<MasterDataPriceListRecord>> SetPriceListLifecycleAsync(
        TenantContext tenantContext,
        Guid priceListId,
        MasterDataLifecycleState lifecycleState,
        byte[] expectedVersion,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        CommitAsync(
            tenantContext,
            evidence,
            "price_list_lifecycle_no_change",
            async db =>
            {
                var entity = await db.PriceLists
                    .Include(item => item.Prices)
                    .SingleOrDefaultAsync(item => item.Id == priceListId, cancellationToken);
                if (entity is null)
                {
                    return MasterDataPersistenceResult<MasterDataPriceListRecord>.Denied(
                        MasterDataPersistenceOutcome.NotFound,
                        "price_list_not_found");
                }

                if (!IsOrganizationVisible(tenantContext, entity))
                {
                    return MasterDataPersistenceResult<MasterDataPriceListRecord>.Denied(
                        MasterDataPersistenceOutcome.Failure,
                        "organization_scope_denied");
                }

                if (!VersionMatches(entity.Version, expectedVersion))
                {
                    return MasterDataPersistenceResult<MasterDataPriceListRecord>.Denied(
                        MasterDataPersistenceOutcome.Conflict,
                        "concurrency_conflict");
                }

                if (entity.LifecycleState == lifecycleState)
                {
                    return MasterDataPersistenceResult<MasterDataPriceListRecord>.Denied(
                        MasterDataPersistenceOutcome.Failure,
                        "price_list_lifecycle_no_change");
                }

                entity.SetLifecycle(lifecycleState);
                return MasterDataPersistenceResult<MasterDataPriceListRecord>.Success(ToRecord(entity));
            },
            cancellationToken);

    public async Task<MasterDataPersistenceResult<MasterDataPriceListReferenceRecord>> ResolvePriceAsync(
        TenantContext tenantContext,
        ResolveMasterDataPriceQuery query,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!AuditContextMatches(tenantContext, evidence))
        {
            return MasterDataPersistenceResult<MasterDataPriceListReferenceRecord>.Denied(
                MasterDataPersistenceOutcome.AuditFailure,
                "audit_context_mismatch");
        }

        await using var db = CreateContext(tenantContext);
        return await ResolvePriceCoreAsync(db, tenantContext, query, cancellationToken);
    }

    private async Task<MasterDataPersistenceResult<MasterDataPriceListReferenceRecord>> ResolvePriceCoreAsync(
        MasterDataDbContext db,
        TenantContext tenantContext,
        ResolveMasterDataPriceQuery query,
        CancellationToken cancellationToken)
    {
                var productExists = await db.Products.AnyAsync(item => item.Id == query.ProductId && item.LifecycleState == MasterDataLifecycleState.Active, cancellationToken);
                if (!productExists)
                {
                    return MasterDataPersistenceResult<MasterDataPriceListReferenceRecord>.Denied(MasterDataPersistenceOutcome.InvalidReference, "price_list_product_not_found");
                }

                var unitExists = await db.UnitsOfMeasure.AnyAsync(item => item.Id == query.UnitOfMeasureId && item.LifecycleState == MasterDataLifecycleState.Active, cancellationToken);
                if (!unitExists)
                {
                    return MasterDataPersistenceResult<MasterDataPriceListReferenceRecord>.Denied(MasterDataPersistenceOutcome.InvalidReference, "price_list_uom_not_found");
                }

                var currencyExists = await db.Currencies.AnyAsync(item => item.Id == query.CurrencyId && item.LifecycleState == MasterDataLifecycleState.Active, cancellationToken);
                if (!currencyExists)
                {
                    return MasterDataPersistenceResult<MasterDataPriceListReferenceRecord>.Denied(MasterDataPersistenceOutcome.InvalidReference, "price_list_currency_not_found");
                }

                var customerCode = await ValidateCustomerReferenceAsync(tenantContext, query.CustomerId, cancellationToken);
                if (customerCode is not null)
                {
                    return MasterDataPersistenceResult<MasterDataPriceListReferenceRecord>.Denied(MasterDataPersistenceOutcome.InvalidReference, customerCode);
                }

                var listsQuery = db.PriceLists
                    .AsNoTracking()
                    .Include(item => item.Prices)
                    .Where(item => item.LifecycleState == MasterDataLifecycleState.Active);
                if (query.PriceListId is { } priceListId)
                {
                    listsQuery = listsQuery.Where(item => item.Id == priceListId);
                }

                var lists = (await listsQuery.ToListAsync(cancellationToken))
                    .Where(item => IsOrganizationVisible(tenantContext, item))
                    .ToArray();
                if (query.PriceListId is { } requestedPriceListId && lists.Length == 0)
                {
                    var requested = await db.PriceLists
                        .AsNoTracking()
                        .SingleOrDefaultAsync(item => item.Id == requestedPriceListId, cancellationToken);
                    var visible = requested is not null && IsOrganizationVisible(tenantContext, requested);
                    return MasterDataPersistenceResult<MasterDataPriceListReferenceRecord>.Denied(
                        visible ? MasterDataPersistenceOutcome.Failure : MasterDataPersistenceOutcome.NotFound,
                        visible ? "price_list_inactive" : "price_list_not_found");
                }

                var candidates = lists
                    .SelectMany(list => list.Prices
                        .Where(price => list.CurrencyId == query.CurrencyId
                            && price.CurrencyId == list.CurrencyId
                            && price.ProductId == query.ProductId
                            && price.UnitOfMeasureId == query.UnitOfMeasureId
                            && price.EffectiveFrom <= query.EffectiveOn
                            && (price.EffectiveTo is null || query.EffectiveOn <= price.EffectiveTo.Value)
                            && CustomerMatches(list.CustomerId, query.CustomerId)
                            && OrganizationMatches(list.OrganizationScopeKind, list.OrganizationScopeId, query.OrganizationScopeKind, query.OrganizationScopeId))
                        .Select(price => (List: list, Price: price)))
                    .ToArray();
                if (candidates.Length == 0)
                {
                    return MasterDataPersistenceResult<MasterDataPriceListReferenceRecord>.Denied(MasterDataPersistenceOutcome.NotFound, "price_list_price_not_found");
                }

                var bestPriority = candidates.Min(item => item.List.Priority);
                var best = candidates.Where(item => item.List.Priority == bestPriority).ToArray();
                if (best.Length != 1)
                {
                    return MasterDataPersistenceResult<MasterDataPriceListReferenceRecord>.Denied(MasterDataPersistenceOutcome.Conflict, "price_list_precedence_conflict");
                }

                var selected = best[0];
                var record = ToPriceRecord(selected.Price);
                var currentConfiguration = ToCurrentConfiguration(selected.List);
                var snapshotValue = $"pl={selected.List.Id:N};v={selected.Price.VersionNumber}";
                var snapshot = new ReferenceSnapshot(
                    MasterDataResourceKind.PriceList,
                    selected.List.Id,
                    new TenantOwnership(tenantContext.TenantId.Value),
                    selected.Price.VersionNumber,
                    snapshotValue,
                    query.EffectiveOn);
                return MasterDataPersistenceResult<MasterDataPriceListReferenceRecord>.Success(
                    new MasterDataPriceListReferenceRecord(
                        selected.List.Id,
                        selected.List.TenantId,
                        selected.List.Code,
                        record,
                        currentConfiguration,
                        query.EffectiveOn,
                        snapshot,
                        selected.List.Version.ToArray()));
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
        Guid? priceListId = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var query = db.AuditEvents
            .AsNoTracking()
            .Where(item => item.ResourceKind == MasterDataResourceKind.PriceList);
        if (priceListId is { } id)
        {
            var priceList = await db.PriceLists
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (priceList is null || !IsOrganizationVisible(tenantContext, priceList))
            {
                return [];
            }

            query = query.Where(item => item.ResourceId == id);
        }
        else
        {
            var visiblePriceListIds = (await db.PriceLists
                    .AsNoTracking()
                    .Select(item => new { item.Id, item.OrganizationScopeKind, item.OrganizationScopeId })
                    .ToListAsync(cancellationToken))
                .Where(item => IsOrganizationRequestAllowed(tenantContext, item.OrganizationScopeKind, item.OrganizationScopeId))
                .Select(item => item.Id)
                .ToArray();
            if (visiblePriceListIds.Length == 0)
            {
                return [];
            }

            query = query.Where(item => item.ResourceId.HasValue && visiblePriceListIds.Contains(item.ResourceId.Value));
        }

        var events = await query.ToListAsync(cancellationToken);
        return events.OrderByDescending(item => item.OccurredAt).Select(ToAuditRecord).ToArray();
    }

    private async Task<bool> HasCrossListPrecedenceConflictAsync(
        MasterDataDbContext db,
        MasterDataPriceListEntity entity,
        AppendMasterDataPriceCommand command,
        CancellationToken cancellationToken)
    {
        var otherLists = await db.PriceLists
            .AsNoTracking()
            .Include(item => item.Prices)
            .Where(item => item.Id != entity.Id
                && item.LifecycleState == MasterDataLifecycleState.Active)
            .ToListAsync(cancellationToken);
        return otherLists.Any(list =>
            list.CurrencyId == entity.CurrencyId
            && CustomerApplicabilityOverlaps(list.CustomerId, entity.CustomerId)
            && OrganizationApplicabilityOverlaps(
                list.OrganizationScopeKind,
                list.OrganizationScopeId,
                entity.OrganizationScopeKind,
                entity.OrganizationScopeId)
            && list.Priority == entity.Priority
            && list.Prices.Any(price =>
                price.CurrencyId == list.CurrencyId
                && price.ProductId == command.ProductId
                && price.UnitOfMeasureId == command.UnitOfMeasureId
                && Overlaps(price.EffectiveFrom, price.EffectiveTo, command.EffectiveFrom, command.EffectiveTo)));
    }

    private static async Task<bool> HasProposedEditPrecedenceConflictAsync(
        MasterDataDbContext db,
        MasterDataPriceListEntity entity,
        EditMasterDataPriceListCommand command,
        CancellationToken cancellationToken)
    {
        var otherLists = await db.PriceLists
            .AsNoTracking()
            .Include(item => item.Prices)
            .Where(item => item.Id != entity.Id
                && item.LifecycleState == MasterDataLifecycleState.Active)
            .ToListAsync(cancellationToken);

        return otherLists.Any(list =>
            list.CurrencyId == command.CurrencyId
            && CustomerApplicabilityOverlaps(list.CustomerId, command.CustomerId)
            && OrganizationApplicabilityOverlaps(
                list.OrganizationScopeKind,
                list.OrganizationScopeId,
                command.OrganizationScopeKind,
                command.OrganizationScopeId)
            && list.Priority == command.Priority
            && entity.Prices.Any(price =>
                price.CurrencyId == command.CurrencyId
                && list.Prices.Any(otherPrice =>
                    otherPrice.CurrencyId == list.CurrencyId
                    && otherPrice.ProductId == price.ProductId
                    && otherPrice.UnitOfMeasureId == price.UnitOfMeasureId
                    && Overlaps(price.EffectiveFrom, price.EffectiveTo, otherPrice.EffectiveFrom, otherPrice.EffectiveTo))));
    }

    private async Task<string?> ValidateCustomerReferenceAsync(
        TenantContext tenantContext,
        Guid? customerId,
        CancellationToken cancellationToken)
    {
        if (customerId is null)
        {
            return null;
        }

        if (customerReader is null)
        {
            return "customer_reference_unavailable";
        }

        try
        {
            var reference = await customerReader.FindCustomerReferenceAsync(tenantContext, customerId.Value, cancellationToken);
            if (reference is null || reference.TenantId.Value != tenantContext.TenantId.Value)
            {
                return "price_list_customer_not_found";
            }

            return reference.LifecycleState == MasterDataLifecycleState.Active
                ? null
                : "price_list_customer_inactive";
        }
        catch
        {
            return "customer_reference_unavailable";
        }
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

        if (!AuditContextMatches(tenantContext, evidence))
        {
            return MasterDataPersistenceResult<T>.Denied(MasterDataPersistenceOutcome.AuditFailure, "audit_context_mismatch");
        }

        await using var db = CreateContext(tenantContext);
        // Conflict prevention spans sibling Price Lists, so the read/check/
        // append unit must not allow two equal-priority candidates to pass
        // concurrently between the check and the insert.
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
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

    private static bool AuditContextMatches(
        TenantContext tenantContext,
        MasterDataAuditEvidence evidence) =>
        evidence.Tenant.TenantId == tenantContext.TenantId.Value
        && evidence.ActorId != Guid.Empty
        && (tenantContext.ActorId is not { } actorId || actorId == evidence.ActorId);

    private static bool IsOrganizationVisible(
        TenantContext tenantContext,
        MasterDataPriceListEntity entity) =>
        IsOrganizationRequestAllowed(
            tenantContext,
            entity.OrganizationScopeKind,
            entity.OrganizationScopeId);

    private static bool IsOrganizationRequestAllowed(
        TenantContext tenantContext,
        OrganizationScopeKind? organizationScopeKind,
        Guid? organizationScopeId)
    {
        if (organizationScopeKind is null && organizationScopeId is null)
        {
            return true;
        }

        return organizationScopeKind is OrganizationScopeKind.Company or OrganizationScopeKind.Branch
            && organizationScopeId is { } organizationId
            && organizationId != Guid.Empty
            && tenantContext.Scope is { } trustedScope
            && string.Equals(
                trustedScope.Value,
                $"{organizationScopeKind}:{organizationId:D}",
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool CustomerMatches(Guid? configuredCustomerId, Guid? requestedCustomerId) =>
        configuredCustomerId is null || requestedCustomerId == configuredCustomerId;

    private static bool CustomerApplicabilityOverlaps(Guid? leftCustomerId, Guid? rightCustomerId) =>
        leftCustomerId is null
        || rightCustomerId is null
        || leftCustomerId == rightCustomerId;

    private static bool OrganizationApplicabilityOverlaps(
        OrganizationScopeKind? leftKind,
        Guid? leftId,
        OrganizationScopeKind? rightKind,
        Guid? rightId) =>
        leftKind is null
        || rightKind is null
        || leftKind == rightKind && leftId == rightId;

    private static bool OrganizationMatches(
        OrganizationScopeKind? configuredKind,
        Guid? configuredId,
        OrganizationScopeKind? requestedKind,
        Guid? requestedId) =>
        configuredKind is null
            || configuredKind == requestedKind && configuredId == requestedId;

    private static bool Overlaps(DateOnly leftFrom, DateOnly? leftTo, DateOnly rightFrom, DateOnly? rightTo) =>
        leftFrom <= (rightTo ?? DateOnly.MaxValue) && rightFrom <= (leftTo ?? DateOnly.MaxValue);

    private static bool VersionMatches(byte[] current, byte[] expected) =>
        expected is not null && current.AsSpan().SequenceEqual(expected);

    private static MasterDataPriceListRecord ToRecord(MasterDataPriceListEntity entity) =>
        new(
            entity.Id,
            entity.TenantId,
            entity.Code,
            entity.Name,
            entity.CurrencyId,
            entity.CurrencyCode,
            entity.CustomerId,
            entity.OrganizationScopeKind,
            entity.OrganizationScopeId,
            entity.Priority,
            entity.LifecycleState,
            entity.CurrentVersionNumber,
            entity.Prices.OrderBy(item => item.VersionNumber).Select(ToPriceRecord).ToArray(),
            entity.Version.ToArray());

    private static MasterDataPriceListPriceRecord ToPriceRecord(MasterDataPriceListPriceEntity entity) =>
        new(
            entity.Id,
            entity.VersionNumber,
            entity.ProductId,
            entity.ProductSku,
            entity.UnitOfMeasureId,
            entity.UnitOfMeasureCode,
            entity.CurrencyId,
            entity.CurrencyCode,
            entity.CustomerId,
            entity.OrganizationScopeKind,
            entity.OrganizationScopeId,
            entity.Priority,
            entity.EffectiveFrom,
            entity.EffectiveTo,
            entity.Price,
            entity.PriceScale,
            entity.Provenance,
            entity.SourceReference,
            entity.Version.ToArray());

    private static MasterDataPriceListCurrentConfiguration ToCurrentConfiguration(MasterDataPriceListEntity entity) =>
        new(
            entity.CurrencyId,
            entity.CurrencyCode,
            entity.CustomerId,
            entity.OrganizationScopeKind,
            entity.OrganizationScopeId,
            entity.Priority,
            entity.LifecycleState);

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
