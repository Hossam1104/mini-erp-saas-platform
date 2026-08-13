#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.MasterData;

/// <summary>
/// MESP-121 Phase A application behavior. Price Lists are reusable
/// Tenant-owned configuration. This service owns authority, validation,
/// deterministic reference selection, and immutable reference evidence; it
/// does not create Sales Orders, promotions, POS behavior, or accounting.
/// </summary>
public sealed class MasterDataPriceListService
{
    private readonly MasterDataResourceAuthorizationService authorization;
    private readonly IMasterDataPriceListPersistence persistence;
    private readonly IBusinessCustomerReferenceReader? customerReader;

    public MasterDataPriceListService(
        MasterDataResourceAuthorizationService authorization,
        IMasterDataPriceListPersistence persistence,
        IBusinessCustomerReferenceReader? customerReader = null)
    {
        this.authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        this.customerReader = customerReader;
    }

    public async Task<MasterDataOperationResult<IReadOnlyList<MasterDataPriceListRecord>>> ListPriceListsAsync(
        MasterDataRequestContext context,
        string? search,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var resource = Resource(context, null, "price-list-list");
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.View);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<IReadOnlyList<MasterDataPriceListRecord>>(context, resource, MasterDataOperation.View, authorized.Decision, authorized.Code, cancellationToken);
        }

        try
        {
            return MasterDataOperationResult<IReadOnlyList<MasterDataPriceListRecord>>.Success(
                await persistence.ListPriceListsAsync(context.TenantContext, search, cancellationToken));
        }
        catch
        {
            return await FailedAsync<IReadOnlyList<MasterDataPriceListRecord>>(context, resource, MasterDataOperation.View, "persistence_unavailable", cancellationToken);
        }
    }

    public Task<MasterDataOperationResult<MasterDataPriceListRecord>> GetPriceListAsync(
        MasterDataRequestContext context,
        Guid priceListId,
        CancellationToken cancellationToken = default) =>
        GetForOperationAsync(context, priceListId, MasterDataOperation.View, cancellationToken);

    public Task<MasterDataOperationResult<MasterDataPriceListRecord>> GetPriceListHistoryAsync(
        MasterDataRequestContext context,
        Guid priceListId,
        CancellationToken cancellationToken = default) =>
        GetForOperationAsync(context, priceListId, MasterDataOperation.View, cancellationToken);

    public async Task<MasterDataOperationResult<MasterDataPriceListRecord>> CreatePriceListAsync(
        MasterDataRequestContext context,
        CreateMasterDataPriceListCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);

        string code;
        LocalizedName name;
        try
        {
            code = MasterDataPriceListValuePolicy.NormalizeCode(command.Code);
            name = command.Name;
            MasterDataPriceListValuePolicy.ValidateApplicability(
                command.CurrencyId,
                command.CustomerId,
                command.OrganizationScopeKind,
                command.OrganizationScopeId,
                command.Priority);
            ValidateOrganizationAuthority(context, command.OrganizationScopeKind, command.OrganizationScopeId);
        }
        catch (ArgumentException)
        {
            return await FailedAsync<MasterDataPriceListRecord>(context, Resource(context, Guid.NewGuid(), "price-list-create"), MasterDataOperation.Create, "validation_failed", cancellationToken);
        }

        var customerCode = await ValidateCustomerAsync(context, command.CustomerId, cancellationToken);
        if (customerCode is not null)
        {
            return await FailedAsync<MasterDataPriceListRecord>(context, Resource(context, Guid.NewGuid(), code), MasterDataOperation.Create, customerCode, cancellationToken);
        }

        var priceListId = Guid.NewGuid();
        var resource = Resource(context, priceListId, code, command.OrganizationScopeKind, command.OrganizationScopeId);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.Create);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataPriceListRecord>(context, resource, MasterDataOperation.Create, authorized.Decision, authorized.Code, cancellationToken);
        }

        var normalized = command with { Code = code, Name = name };
        var evidence = CreateEvidence(
            context,
            resource,
            MasterDataOperation.Create,
            authorized.Decision,
            FoundationAuditReason.Allowed,
            afterSummary: PriceListSummary(priceListId, normalized, MasterDataLifecycleState.Active, 0));

        try
        {
            var result = await persistence.CreatePriceListAsync(context.TenantContext, priceListId, normalized, evidence, cancellationToken);
            return await CompletePersistenceAsync(context, resource, MasterDataOperation.Create, result, evidence, cancellationToken);
        }
        catch
        {
            return await FailedAsync<MasterDataPriceListRecord>(context, resource, MasterDataOperation.Create, "persistence_unavailable", cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<MasterDataPriceListRecord>> EditPriceListAsync(
        MasterDataRequestContext context,
        EditMasterDataPriceListCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);
        var resource = Resource(context, command.PriceListId == Guid.Empty ? Guid.NewGuid() : command.PriceListId, "price-list-edit");
        try
        {
            if (command.PriceListId == Guid.Empty)
            {
                throw new ArgumentException("A Price List identifier is required.");
            }

            MasterDataPriceListValuePolicy.ValidateVersion(command.ExpectedVersion);
            var code = MasterDataPriceListValuePolicy.NormalizeCode(command.Code);
            MasterDataPriceListValuePolicy.ValidateApplicability(command.CurrencyId, command.CustomerId, command.OrganizationScopeKind, command.OrganizationScopeId, command.Priority);
            ValidateOrganizationAuthority(context, command.OrganizationScopeKind, command.OrganizationScopeId);
            resource = Resource(context, command.PriceListId, code, command.OrganizationScopeKind, command.OrganizationScopeId);
        }
        catch (ArgumentException)
        {
            return await FailedAsync<MasterDataPriceListRecord>(context, resource, MasterDataOperation.Edit, "validation_failed", cancellationToken);
        }

        var customerCode = await ValidateCustomerAsync(context, command.CustomerId, cancellationToken);
        if (customerCode is not null)
        {
            return await FailedAsync<MasterDataPriceListRecord>(context, resource, MasterDataOperation.Edit, customerCode, cancellationToken);
        }

        var authorized = authorization.Authorize(context, resource, MasterDataOperation.Edit);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataPriceListRecord>(context, resource, MasterDataOperation.Edit, authorized.Decision, authorized.Code, cancellationToken);
        }

        var evidence = CreateEvidence(
            context,
            resource,
            MasterDataOperation.Edit,
            authorized.Decision,
            FoundationAuditReason.Allowed,
            afterSummary: PriceListSummary(command.PriceListId, command, MasterDataLifecycleState.Active, 0));
        try
        {
            var result = await persistence.EditPriceListAsync(context.TenantContext, command, evidence, cancellationToken);
            return await CompletePersistenceAsync(context, resource, MasterDataOperation.Edit, result, evidence, cancellationToken);
        }
        catch
        {
            return await FailedAsync<MasterDataPriceListRecord>(context, resource, MasterDataOperation.Edit, "persistence_unavailable", cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<MasterDataPriceListRecord>> AppendPriceAsync(
        MasterDataRequestContext context,
        AppendMasterDataPriceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);
        var resource = Resource(context, command.PriceListId == Guid.Empty ? Guid.NewGuid() : command.PriceListId, "price-list-price-append");
        try
        {
            if (command.PriceListId == Guid.Empty)
            {
                throw new ArgumentException("A Price List identifier is required.");
            }

            MasterDataPriceListValuePolicy.ValidateVersion(command.ExpectedVersion);
            MasterDataPriceListValuePolicy.ValidatePrice(command.ProductId, command.UnitOfMeasureId, command.EffectiveFrom, command.EffectiveTo, command.Price, command.PriceScale, command.Provenance, command.SourceReference);
        }
        catch (ArgumentException)
        {
            return await FailedAsync<MasterDataPriceListRecord>(context, resource, MasterDataOperation.Edit, "validation_failed", cancellationToken);
        }

        var authorized = authorization.Authorize(context, resource, MasterDataOperation.Edit);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataPriceListRecord>(context, resource, MasterDataOperation.Edit, authorized.Decision, authorized.Code, cancellationToken);
        }

        var evidence = CreateEvidence(
            context,
            resource,
            MasterDataOperation.Edit,
            authorized.Decision,
            FoundationAuditReason.Allowed,
            afterSummary: PriceSummary(command));
        try
        {
            var result = await persistence.AppendPriceAsync(context.TenantContext, command, evidence, cancellationToken);
            return await CompletePersistenceAsync(context, resource, MasterDataOperation.Edit, result, evidence, cancellationToken);
        }
        catch
        {
            return await FailedAsync<MasterDataPriceListRecord>(context, resource, MasterDataOperation.Edit, "persistence_unavailable", cancellationToken);
        }
    }

    public Task<MasterDataOperationResult<MasterDataPriceListRecord>> DeactivatePriceListAsync(
        MasterDataRequestContext context,
        Guid priceListId,
        byte[] expectedVersion,
        CancellationToken cancellationToken = default) =>
        SetLifecycleAsync(context, priceListId, MasterDataLifecycleState.Inactive, MasterDataOperation.Deactivate, expectedVersion, cancellationToken);

    public Task<MasterDataOperationResult<MasterDataPriceListRecord>> ReactivatePriceListAsync(
        MasterDataRequestContext context,
        Guid priceListId,
        byte[] expectedVersion,
        CancellationToken cancellationToken = default) =>
        SetLifecycleAsync(context, priceListId, MasterDataLifecycleState.Active, MasterDataOperation.Reactivate, expectedVersion, cancellationToken);

    public async Task<MasterDataOperationResult<MasterDataPriceListReferenceRecord>> ResolvePriceAsync(
        MasterDataRequestContext context,
        ResolveMasterDataPriceQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);
        var resource = Resource(context, query.PriceListId, "price-reference");
        try
        {
            if (query.ProductId == Guid.Empty || query.UnitOfMeasureId == Guid.Empty || query.CurrencyId == Guid.Empty || query.EffectiveOn == default)
            {
                throw new ArgumentException("Product, UOM, Currency, and effective date are required.");
            }

            MasterDataPriceListValuePolicy.ValidateApplicability(query.CurrencyId, query.CustomerId, query.OrganizationScopeKind, query.OrganizationScopeId, 0);
            ValidateOrganizationAuthority(context, query.OrganizationScopeKind, query.OrganizationScopeId);
            resource = Resource(context, query.PriceListId, "price-reference", query.OrganizationScopeKind, query.OrganizationScopeId);
        }
        catch (ArgumentException)
        {
            return await FailedAsync<MasterDataPriceListReferenceRecord>(context, resource, MasterDataOperation.View, "validation_failed", cancellationToken);
        }

        var customerCode = await ValidateCustomerAsync(context, query.CustomerId, cancellationToken);
        if (customerCode is not null)
        {
            return await FailedAsync<MasterDataPriceListReferenceRecord>(context, resource, MasterDataOperation.View, customerCode, cancellationToken);
        }

        var authorized = authorization.Authorize(context, resource, MasterDataOperation.View);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataPriceListReferenceRecord>(context, resource, MasterDataOperation.View, authorized.Decision, authorized.Code, cancellationToken);
        }

        var evidence = CreateEvidence(
            context,
            resource,
            MasterDataOperation.View,
            authorized.Decision,
            FoundationAuditReason.Allowed,
            afterSummary: ResolutionSummary(query));
        try
        {
            var result = await persistence.ResolvePriceAsync(context.TenantContext, query, evidence, cancellationToken);
            if (!result.Succeeded || result.Value is null)
            {
                var failure = await FailedAsync<MasterDataPriceListReferenceRecord>(
                    context,
                    resource,
                    MasterDataOperation.View,
                    result.Code,
                    cancellationToken,
                    afterSummary: $"{ResolutionSummary(query)};result={result.Code}");
                return failure with { Evidence = evidence };
            }

            var selected = result.Value;
            var selectedResource = Resource(
                context,
                selected.PriceListId,
                selected.PriceListCode,
                selected.Price.OrganizationScopeKind,
                selected.Price.OrganizationScopeId);
            var selectedEvidence = CreateEvidence(
                context,
                selectedResource,
                MasterDataOperation.View,
                authorized.Decision,
                FoundationAuditReason.Allowed,
                afterSummary: ResolutionResultSummary(query, selected));
            var audit = await persistence.AppendAuditAsync(
                context.TenantContext,
                selectedEvidence,
                cancellationToken);
            return audit.Succeeded
                ? MasterDataOperationResult<MasterDataPriceListReferenceRecord>.Success(selected, selectedEvidence)
                : MasterDataOperationResult<MasterDataPriceListReferenceRecord>.Failure("audit_unavailable", selectedEvidence);
        }
        catch
        {
            return await FailedAsync<MasterDataPriceListReferenceRecord>(context, resource, MasterDataOperation.View, "persistence_unavailable", cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<IReadOnlyList<MasterDataAuditRecord>>> ReadAuditHistoryAsync(
        MasterDataRequestContext context,
        Guid? priceListId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var resource = Resource(context, priceListId, "price-list-audit");
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.ViewAuditHistory);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<IReadOnlyList<MasterDataAuditRecord>>(context, resource, MasterDataOperation.ViewAuditHistory, authorized.Decision, authorized.Code, cancellationToken);
        }

        try
        {
            return MasterDataOperationResult<IReadOnlyList<MasterDataAuditRecord>>.Success(await persistence.ReadAuditHistoryAsync(context.TenantContext, priceListId, cancellationToken));
        }
        catch
        {
            return await FailedAsync<IReadOnlyList<MasterDataAuditRecord>>(context, resource, MasterDataOperation.ViewAuditHistory, "persistence_unavailable", cancellationToken);
        }
    }

    private async Task<MasterDataOperationResult<MasterDataPriceListRecord>> GetForOperationAsync(
        MasterDataRequestContext context,
        Guid priceListId,
        MasterDataOperation operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var resource = Resource(context, priceListId == Guid.Empty ? Guid.NewGuid() : priceListId, "price-list-read");
        if (priceListId == Guid.Empty)
        {
            return await FailedAsync<MasterDataPriceListRecord>(context, resource, operation, "validation_failed", cancellationToken);
        }

        var authorized = authorization.Authorize(context, resource, operation);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataPriceListRecord>(context, resource, operation, authorized.Decision, authorized.Code, cancellationToken);
        }

        try
        {
            var record = await persistence.FindPriceListAsync(context.TenantContext, priceListId, cancellationToken);
            return record is null
                ? await FailedAsync<MasterDataPriceListRecord>(context, resource, operation, "price_list_not_found", cancellationToken)
                : MasterDataOperationResult<MasterDataPriceListRecord>.Success(record);
        }
        catch
        {
            return await FailedAsync<MasterDataPriceListRecord>(context, resource, operation, "persistence_unavailable", cancellationToken);
        }
    }

    private async Task<MasterDataOperationResult<MasterDataPriceListRecord>> SetLifecycleAsync(
        MasterDataRequestContext context,
        Guid priceListId,
        MasterDataLifecycleState lifecycleState,
        MasterDataOperation operation,
        byte[] expectedVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var resource = Resource(context, priceListId == Guid.Empty ? Guid.NewGuid() : priceListId, "price-list-lifecycle");
        try
        {
            if (priceListId == Guid.Empty)
            {
                throw new ArgumentException("A Price List identifier is required.");
            }

            MasterDataPriceListValuePolicy.ValidateVersion(expectedVersion);
        }
        catch (ArgumentException)
        {
            return await FailedAsync<MasterDataPriceListRecord>(context, resource, operation, "validation_failed", cancellationToken);
        }

        var authorized = authorization.Authorize(context, resource, operation);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataPriceListRecord>(context, resource, operation, authorized.Decision, authorized.Code, cancellationToken);
        }

        var evidence = CreateEvidence(context, resource, operation, authorized.Decision, FoundationAuditReason.Allowed, afterSummary: $"state={lifecycleState};expected-version={Convert.ToBase64String(expectedVersion)}");
        try
        {
            var result = await persistence.SetPriceListLifecycleAsync(context.TenantContext, priceListId, lifecycleState, expectedVersion, evidence, cancellationToken);
            return await CompletePersistenceAsync(context, resource, operation, result, evidence, cancellationToken);
        }
        catch
        {
            return await FailedAsync<MasterDataPriceListRecord>(context, resource, operation, "persistence_unavailable", cancellationToken);
        }
    }

    private async Task<string?> ValidateCustomerAsync(MasterDataRequestContext context, Guid? customerId, CancellationToken cancellationToken)
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
            var reference = await customerReader.FindCustomerReferenceAsync(context.TenantContext, customerId.Value, cancellationToken);
            if (reference is null || reference.TenantId.Value != context.TenantId.Value)
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

    private static void ValidateOrganizationAuthority(MasterDataRequestContext context, OrganizationScopeKind? kind, Guid? id)
    {
        if (kind is null && id is null)
        {
            return;
        }

        if (kind is not (OrganizationScopeKind.Company or OrganizationScopeKind.Branch)
            || id is not { } organizationId
            || organizationId == Guid.Empty
            || context.TrustedScope is not { } trustedScope
            || !string.Equals(trustedScope.Value, $"{kind}:{organizationId:D}", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The requested organization applicability is not within the server-derived scope.");
        }
    }

    private async Task<MasterDataOperationResult<T>> CompletePersistenceAsync<T>(
        MasterDataRequestContext context,
        MasterDataResourceReference resource,
        MasterDataOperation operation,
        MasterDataPersistenceResult<T> result,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken)
    {
        if (result.Succeeded && result.Value is not null)
        {
            return MasterDataOperationResult<T>.Success(result.Value, evidence);
        }

        var failure = await FailedAsync<T>(context, resource, operation, result.Code, cancellationToken);
        return failure with { Evidence = evidence };
    }

    private async Task<MasterDataOperationResult<T>> DeniedAsync<T>(MasterDataRequestContext context, MasterDataResourceReference resource, MasterDataOperation operation, MasterDataPolicyDecision decision, string code, CancellationToken cancellationToken)
    {
        var evidence = CreateEvidence(context, resource, operation, decision, ReasonFor(code));
        return await AppendDeniedEvidenceAsync<T>(context, evidence, code, cancellationToken);
    }

    private async Task<MasterDataOperationResult<T>> FailedAsync<T>(MasterDataRequestContext context, MasterDataResourceReference resource, MasterDataOperation operation, string code, CancellationToken cancellationToken, string? afterSummary = null)
    {
        var evidence = CreateEvidence(context, resource, operation, MasterDataPolicyDecision.Denied(code), ReasonFor(code), afterSummary: afterSummary);
        return await AppendDeniedEvidenceAsync<T>(context, evidence, code, cancellationToken);
    }

    private async Task<MasterDataOperationResult<T>> AppendDeniedEvidenceAsync<T>(MasterDataRequestContext context, MasterDataAuditEvidence evidence, string code, CancellationToken cancellationToken)
    {
        try
        {
            var audit = await persistence.AppendAuditAsync(context.TenantContext, evidence, cancellationToken);
            return audit.Succeeded
                ? MasterDataOperationResult<T>.Failure(code, evidence)
                : MasterDataOperationResult<T>.Failure("audit_unavailable", evidence);
        }
        catch
        {
            return MasterDataOperationResult<T>.Failure("audit_unavailable", evidence);
        }
    }

    private static MasterDataResourceReference Resource(MasterDataRequestContext context, Guid? stableId, string? businessCode, OrganizationScopeKind? organizationScopeKind = null, Guid? organizationScopeId = null) =>
        new(MasterDataResourceKind.PriceList, new TenantOwnership(context.TenantId.Value), stableId, businessCode, PriceListScopePolicy.CreateScope(new TenantId(context.TenantId.Value), organizationScopeKind, organizationScopeId));

    private static MasterDataAuditEvidence CreateEvidence(MasterDataRequestContext context, MasterDataResourceReference resource, MasterDataOperation operation, MasterDataPolicyDecision decision, FoundationAuditReason reason, string? beforeSummary = null, string? afterSummary = null) =>
        MasterDataAuditEvidenceFactory.Create(context, resource, operation, decision, reason, beforeSummary, afterSummary);

    private static string PriceListSummary(Guid id, CreateMasterDataPriceListCommand command, MasterDataLifecycleState state, int version) =>
        $"price-list={id:N};code={command.Code};currency={command.CurrencyId:N};customer={command.CustomerId?.ToString("N") ?? "tenant-wide"};organization={OrganizationSummary(command.OrganizationScopeKind, command.OrganizationScopeId)};priority={command.Priority};state={state};version={version}";

    private static string PriceListSummary(Guid id, EditMasterDataPriceListCommand command, MasterDataLifecycleState state, int version) =>
        $"price-list={id:N};code={command.Code};currency={command.CurrencyId:N};customer={command.CustomerId?.ToString("N") ?? "tenant-wide"};organization={OrganizationSummary(command.OrganizationScopeKind, command.OrganizationScopeId)};priority={command.Priority};state={state};version={version}";

    private static string PriceSummary(AppendMasterDataPriceCommand command) =>
        $"price-list={command.PriceListId:N};product={command.ProductId:N};uom={command.UnitOfMeasureId:N};effective={command.EffectiveFrom:yyyy-MM-dd}/{command.EffectiveTo?.ToString("yyyy-MM-dd") ?? "open"};price={command.Price.ToString(System.Globalization.CultureInfo.InvariantCulture)};scale={command.PriceScale};provenance={command.Provenance};source={command.SourceReference ?? string.Empty}";

    private static string ResolutionSummary(ResolveMasterDataPriceQuery query) =>
        $"price-list={query.PriceListId?.ToString("N") ?? "any"};product={query.ProductId:N};uom={query.UnitOfMeasureId:N};currency={query.CurrencyId:N};customer={query.CustomerId?.ToString("N") ?? "none"};organization={OrganizationSummary(query.OrganizationScopeKind, query.OrganizationScopeId)};effective={query.EffectiveOn:yyyy-MM-dd};precedence=priority-ascending;tie=conflict";

    private static string ResolutionResultSummary(
        ResolveMasterDataPriceQuery query,
        MasterDataPriceListReferenceRecord result) =>
        $"{ResolutionSummary(query)};result=resolved;selected-price-list={result.PriceListId:N};selected-code={result.PriceListCode};price-version={result.Price.Id:N}/v{result.Price.VersionNumber};effective-window={result.Price.EffectiveFrom:yyyy-MM-dd}/{result.Price.EffectiveTo?.ToString("yyyy-MM-dd") ?? "open"};price={result.Price.Price.ToString(System.Globalization.CultureInfo.InvariantCulture)};scale={result.Price.PriceScale};priority={result.Price.Priority};provenance={result.Price.Provenance};source={result.Price.SourceReference ?? string.Empty};reference={result.Snapshot.AppliedValue}";

    private static string OrganizationSummary(OrganizationScopeKind? kind, Guid? id) =>
        kind is { } value && id is { } organizationId ? $"{value}:{organizationId:N}" : "tenant-wide";

    private static FoundationAuditReason ReasonFor(string code) => code switch
    {
        "cross_tenant_target_denied" => FoundationAuditReason.CrossTenantTargetDenied,
        "permission_denied" => FoundationAuditReason.PermissionDenied,
        "authorization_denied" or "resource_scope_denied" or "organization_scope_denied" => FoundationAuditReason.AuthorizationDenied,
        "price_list_not_found" or "price_list_customer_not_found" or "price_list_product_not_found" or "price_list_uom_not_found" or "price_list_currency_not_found" => FoundationAuditReason.NotFound,
        "concurrency_conflict" or "price_list_effective_overlap" or "price_list_precedence_conflict" or "price_list_duplicate" => FoundationAuditReason.ConcurrencyConflict,
        "persistence_unavailable" or "audit_unavailable" or "audit_context_mismatch" or "customer_reference_unavailable" => FoundationAuditReason.InternalFailure,
        _ => FoundationAuditReason.ValidationFailed
    };
}

#pragma warning restore CS1591
