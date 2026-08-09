#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.MasterData;

/// <summary>
/// Application behavior for M95-SL-03. Product and Item are intentionally one
/// Release-1 identity; this service owns only Product master-data behavior.
/// </summary>
public sealed class ProductIdentityService
{
    private const int MaximumAuditValueLength = 512;

    private readonly ProductResourceAuthorizationService authorization;
    private readonly IProductIdentityPersistence persistence;
    private readonly IProductBaseUomChangePolicy baseUomChangePolicy;

    public ProductIdentityService(
        ProductResourceAuthorizationService authorization,
        IProductIdentityPersistence persistence,
        IProductBaseUomChangePolicy? baseUomChangePolicy = null)
    {
        this.authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        this.baseUomChangePolicy = baseUomChangePolicy ?? new ProductBaseUomChangePolicyUnavailable();
    }

    public async Task<MasterDataOperationResult<IReadOnlyList<ProductIdentityRecord>>> ListProductsAsync(
        MasterDataRequestContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var resource = Resource(context, null, "product-list");
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.View);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<IReadOnlyList<ProductIdentityRecord>>(
                context,
                resource,
                MasterDataOperation.View,
                authorized.Decision,
                authorized.Code,
                cancellationToken);
        }

        try
        {
            var records = await persistence.ListProductsAsync(
                context.TenantContext,
                cancellationToken);
            return MasterDataOperationResult<IReadOnlyList<ProductIdentityRecord>>.Success(records);
        }
        catch
        {
            return await FailedAsync<IReadOnlyList<ProductIdentityRecord>>(
                context,
                resource,
                MasterDataOperation.View,
                "persistence_unavailable",
                cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<ProductIdentityRecord>> GetProductAsync(
        MasterDataRequestContext context,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (productId == Guid.Empty)
        {
            return await FailedAsync<ProductIdentityRecord>(
                context,
                Resource(context, Guid.NewGuid(), "product-read"),
                MasterDataOperation.View,
                "validation_failed",
                cancellationToken);
        }

        var resource = Resource(context, productId, null);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.View);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<ProductIdentityRecord>(
                context,
                resource,
                MasterDataOperation.View,
                authorized.Decision,
                authorized.Code,
                cancellationToken);
        }

        try
        {
            var record = await persistence.FindProductAsync(
                context.TenantContext,
                productId,
                cancellationToken);
            return record is null
                ? await FailedAsync<ProductIdentityRecord>(
                    context,
                    resource,
                    MasterDataOperation.View,
                    "product_not_found",
                    cancellationToken)
                : MasterDataOperationResult<ProductIdentityRecord>.Success(record);
        }
        catch
        {
            return await FailedAsync<ProductIdentityRecord>(
                context,
                resource,
                MasterDataOperation.View,
                "persistence_unavailable",
                cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<ProductIdentityRecord>> CreateProductAsync(
        MasterDataRequestContext context,
        CreateProductIdentityCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);

        var productId = Guid.NewGuid();
        var resource = Resource(context, productId, "product-create");
        string sku;
        string? description;
        IReadOnlyList<string> barcodes;
        try
        {
            sku = ProductIdentityValuePolicy.NormalizeSku(command.Sku);
            ProductIdentityValuePolicy.ValidateName(command.Name);
            description = ProductIdentityValuePolicy.NormalizeDescription(command.Description);
            barcodes = ProductIdentityValuePolicy.NormalizeBarcodes(command.Barcodes ?? []);
        }
        catch (ArgumentException)
        {
            return await FailedAsync<ProductIdentityRecord>(
                context,
                resource,
                MasterDataOperation.Create,
                "validation_failed",
                cancellationToken);
        }

        resource = Resource(context, productId, sku);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.Create);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<ProductIdentityRecord>(
                context,
                resource,
                MasterDataOperation.Create,
                authorized.Decision,
                authorized.Code,
                cancellationToken);
        }

        ProductReferenceValidation references;
        try
        {
            references = await persistence.ValidateReferencesAsync(
                context.TenantContext,
                command.CategoryId,
                command.BaseUnitOfMeasureId,
                cancellationToken);
        }
        catch
        {
            return await FailedAsync<ProductIdentityRecord>(
                context,
                resource,
                MasterDataOperation.Create,
                "persistence_unavailable",
                cancellationToken);
        }

        if (!references.IsValid)
        {
            return await FailedAsync<ProductIdentityRecord>(
                context,
                resource,
                MasterDataOperation.Create,
                references.Code,
                cancellationToken);
        }

        var normalized = command with
        {
            Sku = sku,
            Description = description,
            Barcodes = barcodes
        };
        var evidence = CreateEvidence(
            context,
            resource,
            MasterDataOperation.Create,
            authorized.Decision,
            FoundationAuditReason.Allowed,
            afterSummary: ProductSummary(normalized, references, MasterDataLifecycleState.Active));

        try
        {
            var result = await persistence.CreateProductAsync(
                context.TenantContext,
                productId,
                normalized,
                references,
                evidence,
                cancellationToken);
            return await CompletePersistenceAsync(
                context,
                resource,
                MasterDataOperation.Create,
                result,
                evidence,
                cancellationToken);
        }
        catch
        {
            return await FailedAsync<ProductIdentityRecord>(
                context,
                resource,
                MasterDataOperation.Create,
                "persistence_unavailable",
                cancellationToken) with { Evidence = evidence };
        }
    }

    public async Task<MasterDataOperationResult<ProductIdentityRecord>> EditProductAsync(
        MasterDataRequestContext context,
        EditProductIdentityCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);

        var resource = Resource(
            context,
            command.ProductId == Guid.Empty ? Guid.NewGuid() : command.ProductId,
            "product-edit");
        string sku;
        string? description;
        IReadOnlyList<string> barcodes;
        try
        {
            if (command.ProductId == Guid.Empty)
            {
                throw new ArgumentException("A Product identifier is required.", nameof(command));
            }

            ValidateVersion(command.ExpectedVersion);
            sku = ProductIdentityValuePolicy.NormalizeSku(command.Sku);
            ProductIdentityValuePolicy.ValidateName(command.Name);
            description = ProductIdentityValuePolicy.NormalizeDescription(command.Description);
            barcodes = ProductIdentityValuePolicy.NormalizeBarcodes(command.Barcodes ?? []);
        }
        catch (ArgumentException)
        {
            return await FailedAsync<ProductIdentityRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                "validation_failed",
                cancellationToken);
        }

        resource = Resource(context, command.ProductId, sku);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.Edit);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<ProductIdentityRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                authorized.Decision,
                authorized.Code,
                cancellationToken);
        }

        ProductIdentityRecord current;
        try
        {
            current = await persistence.FindProductAsync(
                context.TenantContext,
                command.ProductId,
                cancellationToken) ?? throw new ProductIdentityNotFoundException();
        }
        catch (ProductIdentityNotFoundException)
        {
            return await FailedAsync<ProductIdentityRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                "product_not_found",
                cancellationToken);
        }
        catch
        {
            return await FailedAsync<ProductIdentityRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                "persistence_unavailable",
                cancellationToken);
        }

        if (current.TenantId.Value != context.TenantId.Value)
        {
            return await FailedAsync<ProductIdentityRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                "cross_tenant_target_denied",
                cancellationToken);
        }

        var integrity = baseUomChangePolicy.Evaluate(
            context,
            current,
            command.BaseUnitOfMeasureId);
        if (!integrity.Allowed)
        {
            return await FailedAsync<ProductIdentityRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                integrity.Code,
                cancellationToken);
        }

        ProductReferenceValidation references;
        try
        {
            references = await persistence.ValidateReferencesAsync(
                context.TenantContext,
                command.CategoryId,
                command.BaseUnitOfMeasureId,
                cancellationToken);
        }
        catch
        {
            return await FailedAsync<ProductIdentityRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                "persistence_unavailable",
                cancellationToken);
        }

        if (!references.IsValid)
        {
            return await FailedAsync<ProductIdentityRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                references.Code,
                cancellationToken);
        }

        var normalized = command with
        {
            Sku = sku,
            Description = description,
            Barcodes = barcodes
        };
        var evidence = CreateEvidence(
            context,
            resource,
            MasterDataOperation.Edit,
            authorized.Decision,
            FoundationAuditReason.Allowed,
            beforeSummary: ProductSummary(current),
            afterSummary: ProductSummary(normalized, references, current.LifecycleState));

        try
        {
            var result = await persistence.EditProductAsync(
                context.TenantContext,
                normalized,
                references,
                evidence,
                cancellationToken);
            return await CompletePersistenceAsync(
                context,
                resource,
                MasterDataOperation.Edit,
                result,
                evidence,
                cancellationToken);
        }
        catch
        {
            return await FailedAsync<ProductIdentityRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                "persistence_unavailable",
                cancellationToken) with { Evidence = evidence };
        }
    }

    public Task<MasterDataOperationResult<ProductIdentityRecord>> ReactivateProductAsync(
        MasterDataRequestContext context,
        Guid productId,
        byte[] expectedVersion,
        CancellationToken cancellationToken = default) =>
        SetProductLifecycleAsync(
            context,
            productId,
            MasterDataLifecycleState.Active,
            expectedVersion,
            reason: null,
            MasterDataOperation.Reactivate,
            cancellationToken);

    public Task<MasterDataOperationResult<ProductIdentityRecord>> ActivateProductAsync(
        MasterDataRequestContext context,
        Guid productId,
        byte[] expectedVersion,
        CancellationToken cancellationToken = default) =>
        ReactivateProductAsync(context, productId, expectedVersion, cancellationToken);

    public Task<MasterDataOperationResult<ProductIdentityRecord>> DeactivateProductAsync(
        MasterDataRequestContext context,
        Guid productId,
        byte[] expectedVersion,
        string? reason,
        CancellationToken cancellationToken = default) =>
        SetProductLifecycleAsync(
            context,
            productId,
            MasterDataLifecycleState.Inactive,
            expectedVersion,
            reason,
            MasterDataOperation.Deactivate,
            cancellationToken);

    public async Task<MasterDataOperationResult<ProductIdentityRecord>> SetProductLifecycleAsync(
        MasterDataRequestContext context,
        Guid productId,
        MasterDataLifecycleState lifecycleState,
        byte[] expectedVersion,
        string? reason,
        MasterDataOperation operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var resource = Resource(
            context,
            productId == Guid.Empty ? Guid.NewGuid() : productId,
            businessCode: null);
        if (productId == Guid.Empty
            || lifecycleState is not (MasterDataLifecycleState.Active or MasterDataLifecycleState.Inactive)
            || operation is not (MasterDataOperation.Activate
                or MasterDataOperation.Deactivate
                or MasterDataOperation.Reactivate))
        {
            return await FailedAsync<ProductIdentityRecord>(
                context,
                resource,
                operation,
                "validation_failed",
                cancellationToken);
        }

        string? normalizedReason = null;
        if (operation == MasterDataOperation.Deactivate)
        {
            try
            {
                normalizedReason = NormalizeLifecycleReason(reason);
            }
            catch (ArgumentException)
            {
                return await FailedAsync<ProductIdentityRecord>(
                    context,
                    resource,
                    operation,
                    "deactivation_reason_required",
                    cancellationToken);
            }
        }

        try
        {
            ValidateVersion(expectedVersion);
        }
        catch (ArgumentException)
        {
            return await FailedAsync<ProductIdentityRecord>(
                context,
                resource,
                operation,
                "validation_failed",
                cancellationToken);
        }

        var authorized = authorization.Authorize(
            context,
            resource,
            operation == MasterDataOperation.Reactivate
                ? MasterDataOperation.Reactivate
                : operation == MasterDataOperation.Activate
                    ? MasterDataOperation.Activate
                    : MasterDataOperation.Deactivate);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<ProductIdentityRecord>(
                context,
                resource,
                operation,
                authorized.Decision,
                authorized.Code,
                cancellationToken);
        }

        ProductIdentityRecord current;
        try
        {
            current = await persistence.FindProductAsync(
                context.TenantContext,
                productId,
                cancellationToken) ?? throw new ProductIdentityNotFoundException();
        }
        catch (ProductIdentityNotFoundException)
        {
            return await FailedAsync<ProductIdentityRecord>(
                context,
                resource,
                operation,
                "product_not_found",
                cancellationToken);
        }
        catch
        {
            return await FailedAsync<ProductIdentityRecord>(
                context,
                resource,
                operation,
                "persistence_unavailable",
                cancellationToken);
        }

        if (current.LifecycleState == lifecycleState)
        {
            return await FailedAsync<ProductIdentityRecord>(
                context,
                resource,
                operation,
                "product_lifecycle_no_change",
                cancellationToken);
        }

        var afterSummary = $"{ProductSummary(current with { LifecycleState = lifecycleState })};reason={normalizedReason ?? string.Empty}";
        var evidence = CreateEvidence(
            context,
            resource,
            operation,
            authorized.Decision,
            FoundationAuditReason.Allowed,
            beforeSummary: ProductSummary(current),
            afterSummary: afterSummary);

        try
        {
            var result = await persistence.SetProductLifecycleAsync(
                context.TenantContext,
                productId,
                lifecycleState,
                expectedVersion,
                evidence,
                cancellationToken);
            return await CompletePersistenceAsync(
                context,
                resource,
                operation,
                result,
                evidence,
                cancellationToken);
        }
        catch
        {
            return await FailedAsync<ProductIdentityRecord>(
                context,
                resource,
                operation,
                "persistence_unavailable",
                cancellationToken) with { Evidence = evidence };
        }
    }

    public async Task<MasterDataOperationResult<IReadOnlyList<MasterDataAuditRecord>>> ReadAuditHistoryAsync(
        MasterDataRequestContext context,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var resource = Resource(
            context,
            productId == Guid.Empty ? Guid.NewGuid() : productId,
            businessCode: null);
        if (productId == Guid.Empty)
        {
            return await FailedAsync<IReadOnlyList<MasterDataAuditRecord>>(
                context,
                resource,
                MasterDataOperation.ViewAuditHistory,
                "validation_failed",
                cancellationToken);
        }

        var authorized = authorization.Authorize(
            context,
            resource,
            MasterDataOperation.ViewAuditHistory);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<IReadOnlyList<MasterDataAuditRecord>>(
                context,
                resource,
                MasterDataOperation.ViewAuditHistory,
                authorized.Decision,
                authorized.Code,
                cancellationToken);
        }

        try
        {
            var records = await persistence.ReadAuditHistoryAsync(
                context.TenantContext,
                productId,
                cancellationToken);
            return MasterDataOperationResult<IReadOnlyList<MasterDataAuditRecord>>.Success(records);
        }
        catch
        {
            return await FailedAsync<IReadOnlyList<MasterDataAuditRecord>>(
                context,
                resource,
                MasterDataOperation.ViewAuditHistory,
                "persistence_unavailable",
                cancellationToken);
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

        var failed = await FailedAsync<T>(
            context,
            resource,
            operation,
            result.Code,
            cancellationToken);
        return failed with { Evidence = evidence };
    }

    private async Task<MasterDataOperationResult<T>> DeniedAsync<T>(
        MasterDataRequestContext context,
        MasterDataResourceReference resource,
        MasterDataOperation operation,
        MasterDataPolicyDecision decision,
        string code,
        CancellationToken cancellationToken)
    {
        var evidence = CreateEvidence(
            context,
            resource,
            operation,
            decision,
            ReasonFor(code));
        return await AppendDeniedEvidenceAsync<T>(context, evidence, code, cancellationToken);
    }

    private async Task<MasterDataOperationResult<T>> FailedAsync<T>(
        MasterDataRequestContext context,
        MasterDataResourceReference resource,
        MasterDataOperation operation,
        string code,
        CancellationToken cancellationToken)
    {
        var evidence = CreateEvidence(
            context,
            resource,
            operation,
            MasterDataPolicyDecision.Denied(code),
            ReasonFor(code));
        return await AppendDeniedEvidenceAsync<T>(context, evidence, code, cancellationToken);
    }

    private async Task<MasterDataOperationResult<T>> AppendDeniedEvidenceAsync<T>(
        MasterDataRequestContext context,
        MasterDataAuditEvidence evidence,
        string code,
        CancellationToken cancellationToken)
    {
        try
        {
            var audit = await persistence.AppendAuditAsync(
                context.TenantContext,
                evidence,
                cancellationToken);
            return audit.Succeeded
                ? MasterDataOperationResult<T>.Failure(code, evidence)
                : MasterDataOperationResult<T>.Failure("audit_unavailable", evidence);
        }
        catch
        {
            return MasterDataOperationResult<T>.Failure("audit_unavailable", evidence);
        }
    }

    private static MasterDataResourceReference Resource(
        MasterDataRequestContext context,
        Guid? stableId,
        string? businessCode) => new(
        MasterDataResourceKind.Product,
        new TenantOwnership(context.TenantId.Value),
        stableId,
        businessCode,
        ProductScopePolicy.CreateScope(context.TenantId));

    private static MasterDataAuditEvidence CreateEvidence(
        MasterDataRequestContext context,
        MasterDataResourceReference resource,
        MasterDataOperation operation,
        MasterDataPolicyDecision decision,
        FoundationAuditReason reason,
        string? beforeSummary = null,
        string? afterSummary = null) => MasterDataAuditEvidenceFactory.Create(
            context,
            resource,
            operation,
            decision,
            reason,
            beforeSummary,
            afterSummary);

    private static string ProductSummary(ProductIdentityRecord record) =>
        $"sku={record.Sku};en={record.Name.English ?? string.Empty};ar={record.Name.Arabic ?? string.Empty};"
        + $"description={AuditValue(record.Description)};category={record.CategoryId:D};uom={record.BaseUnitOfMeasureId:D};"
        + $"tracking-default={record.TrackingDefaultEnabled};tracking-override={record.TrackingEnabledOverride?.ToString() ?? "absent"};"
        + $"tracking={record.TrackingEnabled};sellable={record.IsSellable};purchasable={record.IsPurchasable};"
        + $"inventory-relevant={record.IsInventoryRelevant};barcodes={record.Barcodes.Count};"
        + $"barcode-values={BarcodeValues(record.Barcodes.Select(item => item.Value))};state={record.LifecycleState}";

    private static string ProductSummary(
        CreateProductIdentityCommand command,
        ProductReferenceValidation references,
        MasterDataLifecycleState lifecycleState) =>
        $"sku={command.Sku};en={command.Name.English ?? string.Empty};ar={command.Name.Arabic ?? string.Empty};"
        + $"description={AuditValue(command.Description)};category={command.CategoryId:D};uom={command.BaseUnitOfMeasureId:D};"
        + $"tracking-default={references.TrackingDefaultEnabled};tracking-override={command.TrackingEnabledOverride?.ToString() ?? "absent"};"
        + $"tracking={command.TrackingEnabledOverride ?? references.TrackingDefaultEnabled};sellable={command.IsSellable};"
        + $"purchasable={command.IsPurchasable};inventory-relevant={command.IsInventoryRelevant};"
        + $"barcodes={command.Barcodes?.Count ?? 0};"
        + $"barcode-values={BarcodeValues(command.Barcodes ?? [])};state={lifecycleState}";

    private static string BarcodeValues(IEnumerable<string> values) =>
        AuditValue(string.Join(",", values));

    private static string AuditValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= MaximumAuditValueLength
            ? value
            : value[..MaximumAuditValueLength] + "...";
    }

    private static string ProductSummary(
        EditProductIdentityCommand command,
        ProductReferenceValidation references,
        MasterDataLifecycleState lifecycleState) =>
        ProductSummary(
            new CreateProductIdentityCommand(
                command.Sku,
                command.Name,
                command.Description,
                command.CategoryId,
                command.BaseUnitOfMeasureId,
                command.Barcodes,
                command.TrackingEnabledOverride,
                command.IsSellable,
                command.IsPurchasable,
                command.IsInventoryRelevant),
            references,
            lifecycleState);

    private static void ValidateVersion(byte[] version)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (version.Length == 0 || version.Length > 64)
        {
            throw new ArgumentException("An optimistic-concurrency version is required.", nameof(version));
        }
    }

    private static string NormalizeLifecycleReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A deactivation reason is required.", nameof(reason));
        }

        var normalized = reason.Trim();
        if (normalized.Length > 512 || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("The deactivation reason is outside the approved bound.", nameof(reason));
        }

        return normalized;
    }

    private static FoundationAuditReason ReasonFor(string code) => code switch
    {
        "cross_tenant_target_denied" => FoundationAuditReason.CrossTenantTargetDenied,
        "permission_denied" => FoundationAuditReason.PermissionDenied,
        "authorization_denied"
            or "resource_scope_denied"
            or "resource_policy_not_configured" => FoundationAuditReason.AuthorizationDenied,
        "product_not_found" => FoundationAuditReason.NotFound,
        "concurrency_conflict" => FoundationAuditReason.ConcurrencyConflict,
        "persistence_unavailable"
            or "reference_persistence_unavailable"
            or "audit_unavailable"
            or "audit_evidence_invalid"
            or "audit_evidence_unavailable"
            or "audit_context_mismatch"
            or "base_uom_integrity_unavailable" => FoundationAuditReason.InternalFailure,
        "validation_failed"
            or "product_reference_invalid"
            or "product_duplicate"
            or "product_barcode_duplicate"
            or "deactivation_reason_required"
            or "product_lifecycle_no_change" => FoundationAuditReason.ValidationFailed,
        _ when code.Contains("inactive", StringComparison.Ordinal)
            || code.Contains("reference", StringComparison.Ordinal) => FoundationAuditReason.ValidationFailed,
        _ => FoundationAuditReason.AuthorizationDenied
    };

    private sealed class ProductIdentityNotFoundException : Exception;
}

#pragma warning restore CS1591
