#pragma warning disable CS1591

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.MasterData;

internal sealed record MasterDataImportExistingIdentity(
    string IdentityKey,
    Guid Id,
    string Code,
    byte[] Version);

internal sealed record MasterDataImportParsedPayload(
    bool Valid,
    bool Quarantine,
    IReadOnlyDictionary<string, string?> NormalizedFields,
    string IdentityKey,
    string Code,
    object? Command,
    IReadOnlyList<MasterDataImportDiagnostic> Diagnostics)
{
    internal static MasterDataImportParsedPayload Rejected(
        IReadOnlyDictionary<string, string?> fields,
        string code,
        string message,
        string? field = null) => new(
        false,
        false,
        fields,
        string.Empty,
        string.Empty,
        null,
        [new MasterDataImportDiagnostic(code, message, field, MasterDataImportDiagnosticSeverity.Error)]);

    internal static MasterDataImportParsedPayload Quarantined(
        IReadOnlyDictionary<string, string?> fields,
        string code,
        string message,
        string? field = null) => new(
        false,
        true,
        fields,
        string.Empty,
        string.Empty,
        null,
        [new MasterDataImportDiagnostic(code, message, field, MasterDataImportDiagnosticSeverity.Warning)]);

    internal static MasterDataImportParsedPayload Success(
        IReadOnlyDictionary<string, string?> fields,
        string identityKey,
        string code,
        object command) => new(true, false, fields, identityKey, code, command, []);
}

/// <summary>
/// Reusable adapter shell. Resource-specific parsers own field and reference
/// rules; this shell owns source normalization, duplicate policy, deterministic
/// identity allocation, and the dry-run/commit separation.
/// </summary>
internal sealed class StructuredMasterDataImportProcessor : IMasterDataImportProcessor
{
    private readonly Func<TenantContext, CancellationToken, Task<IReadOnlyList<MasterDataImportExistingIdentity>>> listExisting;
    private readonly Func<TenantContext, IReadOnlyDictionary<string, string?>, CancellationToken, Task<MasterDataImportParsedPayload>> parse;
    private readonly Func<MasterDataImportProcessingContext, MasterDataImportRowRecord, MasterDataImportParsedPayload, Task<MasterDataImportMutationResult>> commit;
    private readonly Func<MasterDataImportProcessingContext, MasterDataImportParsedPayload, MasterDataImportParsedPayload>? validateParsed;

    internal StructuredMasterDataImportProcessor(
        MasterDataResourceKind resourceKind,
        bool allowsMutableUpdate,
        Func<TenantContext, CancellationToken, Task<IReadOnlyList<MasterDataImportExistingIdentity>>> listExisting,
        Func<TenantContext, IReadOnlyDictionary<string, string?>, CancellationToken, Task<MasterDataImportParsedPayload>> parse,
        Func<MasterDataImportProcessingContext, MasterDataImportRowRecord, MasterDataImportParsedPayload, Task<MasterDataImportMutationResult>> commit,
        Func<MasterDataImportProcessingContext, MasterDataImportParsedPayload, MasterDataImportParsedPayload>? validateParsed = null)
    {
        ResourceKind = resourceKind;
        AllowsMutableUpdate = allowsMutableUpdate;
        this.listExisting = listExisting ?? throw new ArgumentNullException(nameof(listExisting));
        this.parse = parse ?? throw new ArgumentNullException(nameof(parse));
        this.commit = commit ?? throw new ArgumentNullException(nameof(commit));
        this.validateParsed = validateParsed;
    }

    public MasterDataResourceKind ResourceKind { get; }

    public bool AllowsMutableUpdate { get; }

    public async Task<MasterDataImportRowValidationResult> ValidateAsync(
        MasterDataImportProcessingContext context,
        MasterDataImportRowRecord row,
        CancellationToken cancellationToken = default)
    {
        var normalized = MasterDataImportFields.Normalize(row.SourceFields, out var normalizationFailure);
        if (normalizationFailure is not null)
        {
            var failure = normalizationFailure.Value;
            return MasterDataImportRowValidationResult.Rejected(
                normalized,
                failure.Code,
                failure.Message,
                failure.Field);
        }

        MasterDataImportParsedPayload parsed;
        try
        {
            parsed = await parse(context.TenantContext, normalized, cancellationToken);
        }
        catch (ArgumentException)
        {
            return MasterDataImportRowValidationResult.Rejected(
                normalized,
                "import_row_invalid",
                "The row contains an invalid value.",
                null);
        }
        catch
        {
            return MasterDataImportRowValidationResult.Quarantined(
                normalized,
                "import_reference_unavailable",
                "A tenant-owned reference required by this row could not be verified.");
        }

        if (!parsed.Valid)
        {
            return parsed.Quarantine
                ? MasterDataImportRowValidationResult.Quarantined(
                    parsed.NormalizedFields,
                    parsed.Diagnostics[0].Code,
                    parsed.Diagnostics[0].Message,
                    parsed.Diagnostics[0].Field)
                : MasterDataImportRowValidationResult.Rejected(
                    parsed.NormalizedFields,
                    parsed.Diagnostics[0].Code,
                    parsed.Diagnostics[0].Message,
                    parsed.Diagnostics[0].Field);
        }

        if (validateParsed is not null)
        {
            parsed = validateParsed(context, parsed);
            if (!parsed.Valid)
            {
                return parsed.Quarantine
                    ? MasterDataImportRowValidationResult.Quarantined(
                        parsed.NormalizedFields,
                        parsed.Diagnostics[0].Code,
                        parsed.Diagnostics[0].Message,
                        parsed.Diagnostics[0].Field)
                    : MasterDataImportRowValidationResult.Rejected(
                        parsed.NormalizedFields,
                        parsed.Diagnostics[0].Code,
                        parsed.Diagnostics[0].Message,
                        parsed.Diagnostics[0].Field);
            }
        }

        IReadOnlyList<MasterDataImportExistingIdentity> existing;
        try
        {
            existing = await listExisting(context.TenantContext, cancellationToken);
        }
        catch
        {
            return MasterDataImportRowValidationResult.Quarantined(
                parsed.NormalizedFields,
                "import_reference_unavailable",
                "The tenant-owned target catalogue could not be read.");
        }

        var existingTarget = existing.SingleOrDefault(item =>
            string.Equals(item.IdentityKey, parsed.IdentityKey, StringComparison.Ordinal));
        if (!context.PriorIdentityKeys.Add(parsed.IdentityKey))
        {
            return MasterDataImportRowValidationResult.Rejected(
                parsed.NormalizedFields,
                "import_duplicate",
                "The identity occurs more than once in this import batch.");
        }

        if (existingTarget is not null)
        {
            return context.Batch.DuplicatePolicy switch
            {
                MasterDataImportDuplicatePolicy.Reject =>
                    MasterDataImportRowValidationResult.Rejected(
                        parsed.NormalizedFields,
                        "import_duplicate",
                        "The tenant already contains this business identity."),
                MasterDataImportDuplicatePolicy.SkipExisting =>
                    MasterDataImportRowValidationResult.Accepted(
                        MasterDataImportFields.WithIdentity(parsed.NormalizedFields, parsed.IdentityKey),
                        parsed.IdentityKey,
                        MasterDataImportMutationDisposition.SkippedExisting,
                        existingTarget.Id,
                        existingTarget.Code,
                        existingTarget.Version),
                MasterDataImportDuplicatePolicy.UpdateMutableFields when AllowsMutableUpdate =>
                    MasterDataImportRowValidationResult.Accepted(
                        MasterDataImportFields.WithIdentity(parsed.NormalizedFields, parsed.IdentityKey),
                        parsed.IdentityKey,
                        MasterDataImportMutationDisposition.Eligible,
                        existingTarget.Id,
                        existingTarget.Code,
                        existingTarget.Version),
                _ => MasterDataImportRowValidationResult.Quarantined(
                    parsed.NormalizedFields,
                    "import_update_not_permitted",
                    "The selected duplicate policy is not permitted for this resource.")
            };
        }

        return MasterDataImportRowValidationResult.Accepted(
            MasterDataImportFields.WithIdentity(parsed.NormalizedFields, parsed.IdentityKey),
            parsed.IdentityKey,
            MasterDataImportMutationDisposition.Eligible,
            MasterDataImportIdentity.DeterministicResourceId(context.Batch.Id, row),
            parsed.Code);
    }

    public async Task<MasterDataImportMutationResult> CommitAsync(
        MasterDataImportProcessingContext context,
        MasterDataImportRowRecord row,
        CancellationToken cancellationToken = default)
    {
        if (row.MutationDisposition == MasterDataImportMutationDisposition.SkippedExisting)
        {
            return MasterDataImportMutationResult.Skipped(
                row.ResultingResourceId,
                row.ResultingResourceCode);
        }

        MasterDataImportParsedPayload parsed;
        try
        {
            parsed = await parse(context.TenantContext, row.NormalizedFields, cancellationToken);
        }
        catch
        {
            return MasterDataImportMutationResult.Failed(
                "import_commit_reparse_failed",
                "The validated row could not be reconstructed for execution.");
        }

        if (!parsed.Valid || parsed.Command is null)
        {
            return MasterDataImportMutationResult.Failed(
                "import_commit_reparse_failed",
                "The validated row could not be reconstructed for execution.");
        }

        try
        {
            return await commit(context, row, parsed);
        }
        catch
        {
            return MasterDataImportMutationResult.Failed(
                "import_commit_failed",
                "The target persistence operation failed without a committed result.");
        }
    }
}

public static class MasterDataImportProcessorFactory
{
    public static IReadOnlyList<IMasterDataImportProcessor> Create(
        IMasterDataCatalogPersistence catalog,
        IProductIdentityPersistence products,
        IMasterDataCurrencyPaymentTermPersistence currencyPaymentTerms,
        IMasterDataTaxPersistence taxes,
        IMasterDataExchangeRatePersistence exchangeRates,
        IMasterDataPriceListPersistence priceLists,
        ISupplierPersistence suppliers,
        ICustomerPersistence customers)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(currencyPaymentTerms);
        ArgumentNullException.ThrowIfNull(taxes);
        ArgumentNullException.ThrowIfNull(exchangeRates);
        ArgumentNullException.ThrowIfNull(priceLists);
        ArgumentNullException.ThrowIfNull(suppliers);
        ArgumentNullException.ThrowIfNull(customers);

        return
        [
            Category(catalog),
            UnitOfMeasure(catalog),
            Product(catalog, products),
            Supplier(suppliers),
            Customer(customers),
            Currency(currencyPaymentTerms),
            PaymentTerm(currencyPaymentTerms),
            Tax(taxes),
            ExchangeRate(currencyPaymentTerms, exchangeRates),
            PriceList(catalog, products, currencyPaymentTerms, priceLists, customers)
        ];
    }

    private static IMasterDataImportProcessor Category(IMasterDataCatalogPersistence persistence) =>
        new StructuredMasterDataImportProcessor(
            MasterDataResourceKind.ProductCategory,
            allowsMutableUpdate: true,
            async (tenant, cancellationToken) =>
                (await persistence.ListCategoriesAsync(tenant, cancellationToken))
                .Select(item => new MasterDataImportExistingIdentity(
                    Identity("category", item.Code), item.Id, item.Code, item.Version))
                .ToArray(),
            async (tenant, fields, cancellationToken) =>
            {
                var common = ParseCodeAndName(fields, requireEnglish: false);
                if (!common.Valid)
                {
                    return MasterDataImportParsedPayload.Rejected(fields, "import_row_invalid", common.Error!);
                }

                string code;
                try
                {
                    code = MasterDataCategoryUomValuePolicy.NormalizeCode(common.Code);
                }
                catch (ArgumentException)
                {
                    return MasterDataImportParsedPayload.Rejected(fields, "import_field_invalid", "The category code is outside the approved bound.", "code");
                }

                var parent = MasterDataImportFields.OptionalGuid(fields, "parentcategoryid");
                if (parent.Error is not null)
                {
                    return MasterDataImportParsedPayload.Rejected(fields, "import_field_invalid", parent.Error, "parentCategoryId");
                }

                if (parent.Value is { } parentId
                    && await persistence.FindCategoryAsync(tenant, parentId, cancellationToken) is null)
                {
                    return MasterDataImportParsedPayload.Quarantined(fields, "import_reference_invalid", "The parent category is not available in this Tenant.", "parentCategoryId");
                }

                var tracking = MasterDataImportFields.OptionalBool(fields, "trackingdefaultenabled", false);
                if (tracking.Error is not null)
                {
                    return MasterDataImportParsedPayload.Rejected(fields, "import_field_invalid", tracking.Error, "trackingDefaultEnabled");
                }

                var command = new CreateMasterDataCategoryCommand(
                    code,
                    common.Name!,
                    parent.Value,
                    tracking.Value);
                return MasterDataImportParsedPayload.Success(
                    fields,
                    Identity("category", code),
                    code,
                    command);
            },
            async (context, row, parsed) =>
            {
                var command = (CreateMasterDataCategoryCommand)parsed.Command!;
                var evidence = Evidence(context, row, MasterDataResourceKind.ProductCategory, command.Code);
                MasterDataPersistenceResult<MasterDataCategoryRecord> result;
                if (row.MutationDisposition == MasterDataImportMutationDisposition.Eligible
                    && row.ExpectedResourceVersion is { } expected
                    && row.ResultingResourceId is { } existingId
                    && !existingId.Equals(MasterDataImportIdentity.DeterministicResourceId(context.Batch.Id, row)))
                {
                    result = await persistence.EditCategoryAsync(
                        context.TenantContext,
                        new EditMasterDataCategoryCommand(
                            existingId,
                            command.Code,
                            command.Name,
                            command.ParentCategoryId,
                            expected,
                            command.TrackingDefaultEnabled),
                        evidence);
                    return Mutation(result, row, updated: true);
                }

                var id = row.ResultingResourceId ?? MasterDataImportIdentity.DeterministicResourceId(context.Batch.Id, row);
                result = await persistence.CreateCategoryAsync(context.TenantContext, id, command, evidence);
                return Mutation(result, row, updated: false);
            });

    private static IMasterDataImportProcessor UnitOfMeasure(IMasterDataCatalogPersistence persistence) =>
        new StructuredMasterDataImportProcessor(
            MasterDataResourceKind.UnitOfMeasure,
            allowsMutableUpdate: true,
            async (tenant, cancellationToken) =>
                (await persistence.ListUnitsOfMeasureAsync(tenant, cancellationToken))
                .Select(item => new MasterDataImportExistingIdentity(
                    Identity("uom", item.Code), item.Id, item.Code, item.Version))
                .ToArray(),
            (tenant, fields, _) =>
            {
                var common = ParseCodeAndName(fields, requireEnglish: false);
                if (!common.Valid)
                {
                    return Task.FromResult<MasterDataImportParsedPayload>(
                        MasterDataImportParsedPayload.Rejected(fields, "import_row_invalid", common.Error!));
                }

                try
                {
                    var code = MasterDataCategoryUomValuePolicy.NormalizeCode(common.Code);
                    return Task.FromResult(MasterDataImportParsedPayload.Success(
                        fields,
                        Identity("uom", code),
                        code,
                        new CreateMasterDataUnitOfMeasureCommand(code, common.Name!)));
                }
                catch (ArgumentException)
                {
                    return Task.FromResult(MasterDataImportParsedPayload.Rejected(fields, "import_field_invalid", "The unit-of-measure code is outside the approved bound.", "code"));
                }
            },
            async (context, row, parsed) =>
            {
                var command = (CreateMasterDataUnitOfMeasureCommand)parsed.Command!;
                var evidence = Evidence(context, row, MasterDataResourceKind.UnitOfMeasure, command.Code);
                if (row.ExpectedResourceVersion is { } expected
                    && row.ResultingResourceId is { } existingId
                    && !existingId.Equals(MasterDataImportIdentity.DeterministicResourceId(context.Batch.Id, row)))
                {
                    var edited = await persistence.EditUnitOfMeasureAsync(
                        context.TenantContext,
                        new EditMasterDataUnitOfMeasureCommand(existingId, command.Code, command.Name, expected),
                        evidence);
                    return Mutation(edited, row, updated: true);
                }

                var created = await persistence.CreateUnitOfMeasureAsync(
                    context.TenantContext,
                    row.ResultingResourceId ?? MasterDataImportIdentity.DeterministicResourceId(context.Batch.Id, row),
                    command,
                    evidence);
                return Mutation(created, row, updated: false);
            });

    private static IMasterDataImportProcessor Product(
        IMasterDataCatalogPersistence catalog,
        IProductIdentityPersistence persistence) =>
        new StructuredMasterDataImportProcessor(
            MasterDataResourceKind.Product,
            allowsMutableUpdate: true,
            async (tenant, cancellationToken) =>
                (await persistence.ListProductsAsync(tenant, cancellationToken))
                .Select(item => new MasterDataImportExistingIdentity(
                    Identity("product", item.Sku), item.Id, item.Sku, item.Version))
                .ToArray(),
            async (tenant, fields, cancellationToken) =>
            {
                var sku = MasterDataImportFields.Required(fields, "sku");
                var common = ParseName(fields, requireEnglish: true);
                if (sku.Error is not null)
                {
                    return MasterDataImportParsedPayload.Rejected(fields, "import_field_required", sku.Error, "sku");
                }

                if (!common.Valid)
                {
                    return MasterDataImportParsedPayload.Rejected(fields, "import_row_invalid", common.Error!);
                }

                var barcodes = MasterDataImportFields.OptionalStringList(fields, "barcodes");
                if (barcodes.Error is not null)
                {
                    return MasterDataImportParsedPayload.Rejected(fields, "import_field_invalid", barcodes.Error, "barcodes");
                }

                string normalizedSku;
                string? normalizedDescription;
                IReadOnlyList<string>? normalizedBarcodes;
                try
                {
                    normalizedSku = ProductIdentityValuePolicy.NormalizeSku(sku.Value!);
                    ProductIdentityValuePolicy.ValidateName(common.Name!);
                    normalizedDescription = ProductIdentityValuePolicy.NormalizeDescription(MasterDataImportFields.Optional(fields, "description"));
                    normalizedBarcodes = barcodes.Value is null
                        ? null
                        : ProductIdentityValuePolicy.NormalizeBarcodes(barcodes.Value);
                }
                catch (ArgumentException)
                {
                    return MasterDataImportParsedPayload.Rejected(fields, "import_field_invalid", "The Product identity or mutable fields are outside the approved bound.");
                }

                var category = MasterDataImportFields.RequiredGuid(fields, "categoryid");
                var unit = MasterDataImportFields.RequiredGuid(fields, "baseunitofmeasureid");
                if (category.Error is not null || unit.Error is not null)
                {
                    return MasterDataImportParsedPayload.Rejected(
                        fields,
                        "import_reference_invalid",
                        category.Error ?? unit.Error!,
                        category.Error is not null ? "categoryId" : "baseUnitOfMeasureId");
                }

                var references = await persistence.ValidateReferencesAsync(tenant, category.Value, unit.Value, cancellationToken);
                if (!references.IsValid)
                {
                    return MasterDataImportParsedPayload.Quarantined(fields, "import_reference_invalid", "Product references must be active records in this Tenant.");
                }

                var tracking = MasterDataImportFields.OptionalNullableBool(fields, "trackingenabledoverride");
                var sellable = MasterDataImportFields.OptionalBool(fields, "issellable", false);
                var purchasable = MasterDataImportFields.OptionalBool(fields, "ispurchasable", false);
                var inventory = MasterDataImportFields.OptionalBool(fields, "isinventoryrelevant", false);
                var boolError = tracking.Error ?? sellable.Error ?? purchasable.Error ?? inventory.Error;
                if (boolError is not null)
                {
                    return MasterDataImportParsedPayload.Rejected(fields, "import_field_invalid", boolError);
                }

                var command = new CreateProductIdentityCommand(
                    normalizedSku,
                    common.Name!,
                    normalizedDescription,
                    category.Value,
                    unit.Value,
                    normalizedBarcodes,
                    tracking.Value,
                    sellable.Value,
                    purchasable.Value,
                    inventory.Value);
                return MasterDataImportParsedPayload.Success(fields, Identity("product", normalizedSku), normalizedSku, command);
            },
            async (context, row, parsed) =>
            {
                var command = (CreateProductIdentityCommand)parsed.Command!;
                var references = await persistence.ValidateReferencesAsync(
                    context.TenantContext,
                    command.CategoryId,
                    command.BaseUnitOfMeasureId);
                var evidence = Evidence(context, row, MasterDataResourceKind.Product, command.Sku);
                if (row.ExpectedResourceVersion is { } expected
                    && row.ResultingResourceId is { } existingId
                    && !existingId.Equals(MasterDataImportIdentity.DeterministicResourceId(context.Batch.Id, row)))
                {
                    var edited = await persistence.EditProductAsync(
                        context.TenantContext,
                        new EditProductIdentityCommand(
                            existingId,
                            command.Sku,
                            command.Name,
                            command.Description,
                            command.CategoryId,
                            command.BaseUnitOfMeasureId,
                            command.Barcodes,
                            command.TrackingEnabledOverride,
                            command.IsSellable,
                            command.IsPurchasable,
                            command.IsInventoryRelevant,
                            expected),
                        references,
                        evidence);
                    return Mutation(edited, row, updated: true);
                }

                var created = await persistence.CreateProductAsync(
                    context.TenantContext,
                    row.ResultingResourceId ?? MasterDataImportIdentity.DeterministicResourceId(context.Batch.Id, row),
                    command,
                    references,
                    evidence);
                return Mutation(created, row, updated: false);
            });

    private static IMasterDataImportProcessor Supplier(ISupplierPersistence persistence) =>
        new StructuredMasterDataImportProcessor(
            MasterDataResourceKind.Supplier,
            allowsMutableUpdate: true,
            async (tenant, cancellationToken) =>
                (await persistence.ListSuppliersAsync(tenant, cancellationToken))
                .Select(item => new MasterDataImportExistingIdentity(
                    Identity("supplier", item.Code), item.Id, item.Code, item.Version))
                .ToArray(),
            (tenant, fields, _) => Task.FromResult(ParseSupplier(fields)),
            async (context, row, parsed) =>
            {
                var command = (CreateSupplierCommand)parsed.Command!;
                var evidence = Evidence(context, row, MasterDataResourceKind.Supplier, command.Code);
                if (row.ExpectedResourceVersion is { } expected
                    && row.ResultingResourceId is { } existingId
                    && !existingId.Equals(MasterDataImportIdentity.DeterministicResourceId(context.Batch.Id, row)))
                {
                    var edited = await persistence.EditSupplierAsync(
                        context.TenantContext,
                        new EditSupplierCommand(existingId, command.Code, command.LegalName, command.TradingName, command.RegistrationReference, command.Contacts, expected),
                        evidence);
                    return Mutation(edited, row, updated: true);
                }

                var created = await persistence.CreateSupplierAsync(
                    context.TenantContext,
                    row.ResultingResourceId ?? MasterDataImportIdentity.DeterministicResourceId(context.Batch.Id, row),
                    command,
                    evidence);
                return Mutation(created, row, updated: false);
            });

    private static IMasterDataImportProcessor Customer(ICustomerPersistence persistence) =>
        new StructuredMasterDataImportProcessor(
            MasterDataResourceKind.BusinessCustomer,
            allowsMutableUpdate: true,
            async (tenant, cancellationToken) =>
                (await persistence.ListCustomersAsync(tenant, cancellationToken))
                .Select(item => new MasterDataImportExistingIdentity(
                    Identity("customer", item.Code), item.Id, item.Code, item.Version))
                .ToArray(),
            (tenant, fields, _) => Task.FromResult(ParseCustomer(fields)),
            async (context, row, parsed) =>
            {
                var command = (CreateCustomerCommand)parsed.Command!;
                var evidence = Evidence(context, row, MasterDataResourceKind.BusinessCustomer, command.Code);
                if (row.ExpectedResourceVersion is { } expected
                    && row.ResultingResourceId is { } existingId
                    && !existingId.Equals(MasterDataImportIdentity.DeterministicResourceId(context.Batch.Id, row)))
                {
                    var edited = await persistence.EditCustomerAsync(
                        context.TenantContext,
                        new EditCustomerCommand(existingId, command.Code, command.LegalName, command.TradingName, command.Contacts, expected),
                        evidence);
                    return Mutation(edited, row, updated: true);
                }

                var created = await persistence.CreateCustomerAsync(
                    context.TenantContext,
                    row.ResultingResourceId ?? MasterDataImportIdentity.DeterministicResourceId(context.Batch.Id, row),
                    command,
                    evidence);
                return Mutation(created, row, updated: false);
            });

    private static IMasterDataImportProcessor Currency(IMasterDataCurrencyPaymentTermPersistence persistence) =>
        new StructuredMasterDataImportProcessor(
            MasterDataResourceKind.Currency,
            allowsMutableUpdate: true,
            async (tenant, cancellationToken) =>
                (await persistence.ListCurrenciesAsync(tenant, cancellationToken))
                .Select(item => new MasterDataImportExistingIdentity(
                    Identity("currency", item.Code), item.Id, item.Code, item.Version))
                .ToArray(),
            (tenant, fields, _) =>
            {
                var common = ParseCodeAndName(fields, requireEnglish: false);
                MasterDataImportParsedPayload result = !common.Valid
                    ? MasterDataImportParsedPayload.Rejected(fields, "import_row_invalid", common.Error!)
                    : CreateCurrencyPayload(fields, common);
                return Task.FromResult(result);
            },
            async (context, row, parsed) =>
            {
                var command = (CreateMasterDataCurrencyCommand)parsed.Command!;
                var evidence = Evidence(context, row, MasterDataResourceKind.Currency, command.Code);
                if (row.ExpectedResourceVersion is { } expected
                    && row.ResultingResourceId is { } existingId
                    && !existingId.Equals(MasterDataImportIdentity.DeterministicResourceId(context.Batch.Id, row)))
                {
                    var edited = await persistence.EditCurrencyAsync(
                        context.TenantContext,
                        new EditMasterDataCurrencyCommand(existingId, command.Code, command.Name, expected),
                        evidence);
                    return Mutation(edited, row, updated: true);
                }

                var created = await persistence.CreateCurrencyAsync(
                    context.TenantContext,
                    row.ResultingResourceId ?? MasterDataImportIdentity.DeterministicResourceId(context.Batch.Id, row),
                    command,
                    evidence);
                return Mutation(created, row, updated: false);
            });

    private static MasterDataImportParsedPayload CreateCurrencyPayload(
        IReadOnlyDictionary<string, string?> fields,
        (bool Valid, string Code, LocalizedName? Name, string? Error) common)
    {
        try
        {
            var code = MasterDataCurrencyPaymentTermValuePolicy.NormalizeCode(common.Code);
            return MasterDataImportParsedPayload.Success(
                fields,
                Identity("currency", code),
                code,
                new CreateMasterDataCurrencyCommand(code, common.Name!));
        }
        catch (ArgumentException)
        {
            return MasterDataImportParsedPayload.Rejected(fields, "import_field_invalid", "The Currency code is outside the approved bound.", "code");
        }
    }

    private static IMasterDataImportProcessor PaymentTerm(IMasterDataCurrencyPaymentTermPersistence persistence) =>
        new StructuredMasterDataImportProcessor(
            MasterDataResourceKind.PaymentTerm,
            allowsMutableUpdate: false,
            async (tenant, cancellationToken) =>
                (await persistence.ListPaymentTermsAsync(tenant, cancellationToken))
                .Select(item => new MasterDataImportExistingIdentity(
                    Identity("payment-term", item.Code), item.Id, item.Code, item.Version))
                .ToArray(),
            (tenant, fields, _) => Task.FromResult(ParsePaymentTerm(fields)),
            async (context, row, parsed) =>
            {
                var command = (CreateMasterDataPaymentTermCommand)parsed.Command!;
                var evidence = Evidence(context, row, MasterDataResourceKind.PaymentTerm, command.Code);
                var created = await persistence.CreatePaymentTermAsync(
                    context.TenantContext,
                    row.ResultingResourceId ?? MasterDataImportIdentity.DeterministicResourceId(context.Batch.Id, row),
                    command,
                    evidence);
                return Mutation(created, row, updated: false);
            });

    private static IMasterDataImportProcessor Tax(IMasterDataTaxPersistence persistence) =>
        new StructuredMasterDataImportProcessor(
            MasterDataResourceKind.Tax,
            allowsMutableUpdate: false,
            async (tenant, cancellationToken) =>
                (await persistence.ListTaxesAsync(tenant, cancellationToken))
                .Select(item => new MasterDataImportExistingIdentity(
                    Identity("tax", item.Code), item.Id, item.Code, item.Version))
                .ToArray(),
            (tenant, fields, _) => Task.FromResult(ParseTax(fields)),
            async (context, row, parsed) =>
            {
                var command = (CreateMasterDataTaxCommand)parsed.Command!;
                var evidence = Evidence(context, row, MasterDataResourceKind.Tax, command.Code);
                var created = await persistence.CreateTaxAsync(
                    context.TenantContext,
                    row.ResultingResourceId ?? MasterDataImportIdentity.DeterministicResourceId(context.Batch.Id, row),
                    command,
                    evidence);
                return Mutation(created, row, updated: false);
            });

    private static IMasterDataImportProcessor ExchangeRate(
        IMasterDataCurrencyPaymentTermPersistence currencyPersistence,
        IMasterDataExchangeRatePersistence persistence) =>
        new StructuredMasterDataImportProcessor(
            MasterDataResourceKind.ExchangeRate,
            allowsMutableUpdate: false,
            async (tenant, cancellationToken) =>
                (await persistence.ListExchangeRatesAsync(tenant, cancellationToken))
                .Select(item => new MasterDataImportExistingIdentity(
                    Identity("exchange-rate", $"{item.SourceCurrencyId:D}:{item.TargetCurrencyId:D}"),
                    item.Id,
                    $"{item.SourceCurrencyId:D}:{item.TargetCurrencyId:D}",
                    item.Version))
                .ToArray(),
            async (tenant, fields, cancellationToken) =>
            {
                var source = MasterDataImportFields.RequiredGuid(fields, "sourcecurrencyid");
                var target = MasterDataImportFields.RequiredGuid(fields, "targetcurrencyid");
                if (source.Error is not null || target.Error is not null)
                {
                    return MasterDataImportParsedPayload.Rejected(fields, "import_reference_invalid", source.Error ?? target.Error!);
                }

                if (await currencyPersistence.FindCurrencyAsync(tenant, source.Value, cancellationToken) is null
                    || await currencyPersistence.FindCurrencyAsync(tenant, target.Value, cancellationToken) is null)
                {
                    return MasterDataImportParsedPayload.Quarantined(fields, "import_reference_invalid", "Both exchange-rate currencies must be available in this Tenant.");
                }

                var from = MasterDataImportFields.RequiredDate(fields, "effectivefrom");
                var to = MasterDataImportFields.OptionalDate(fields, "effectiveto");
                var rate = MasterDataImportFields.RequiredDecimal(fields, "rate");
                if (from.Error is not null || to.Error is not null || rate.Error is not null)
                {
                    return MasterDataImportParsedPayload.Rejected(fields, "import_field_invalid", from.Error ?? to.Error ?? rate.Error!);
                }

                var scale = MasterDataImportFields.OptionalInt(fields, "ratescale", 6);
                var provenance = MasterDataImportFields.OptionalEnum(fields, "provenance", ExchangeRateProvenance.Manual);
                if (scale.Error is not null || provenance.Error is not null)
                {
                    return MasterDataImportParsedPayload.Rejected(fields, "import_field_invalid", scale.Error ?? provenance.Error!);
                }

                try
                {
                    var sourceNotes = MasterDataExchangeRateValuePolicy.NormalizeSourceNotes(MasterDataImportFields.Optional(fields, "sourcenotes"));
                    MasterDataExchangeRateValuePolicy.Validate(
                        source.Value,
                        target.Value,
                        from.Value,
                        to.Value,
                        rate.Value,
                        scale.Value,
                        provenance.Value,
                        sourceNotes);
                    var command = new CreateMasterDataExchangeRateCommand(
                        source.Value,
                        target.Value,
                        from.Value,
                        to.Value,
                        rate.Value,
                        scale.Value,
                        provenance.Value,
                        sourceNotes);
                    var identity = $"{source.Value:D}:{target.Value:D}";
                    return MasterDataImportParsedPayload.Success(fields, Identity("exchange-rate", identity), identity, command);
                }
                catch (ArgumentException)
                {
                    return MasterDataImportParsedPayload.Rejected(fields, "import_field_invalid", "The Exchange Rate identity or value is outside the approved bound.");
                }
            },
            async (context, row, parsed) =>
            {
                var command = (CreateMasterDataExchangeRateCommand)parsed.Command!;
                var evidence = Evidence(context, row, MasterDataResourceKind.ExchangeRate, parsed.Code);
                var created = await persistence.CreateExchangeRateAsync(
                    context.TenantContext,
                    row.ResultingResourceId ?? MasterDataImportIdentity.DeterministicResourceId(context.Batch.Id, row),
                    command,
                    evidence);
                return Mutation(created, row, updated: false);
            });

    private static IMasterDataImportProcessor PriceList(
        IMasterDataCatalogPersistence catalog,
        IProductIdentityPersistence products,
        IMasterDataCurrencyPaymentTermPersistence currencies,
        IMasterDataPriceListPersistence persistence,
        ICustomerPersistence customers) =>
        new StructuredMasterDataImportProcessor(
            MasterDataResourceKind.PriceList,
            allowsMutableUpdate: true,
            async (tenant, cancellationToken) =>
                (await persistence.ListPriceListsAsync(tenant, null, cancellationToken))
                .Select(item => new MasterDataImportExistingIdentity(
                    Identity("price-list", item.Code), item.Id, item.Code, item.Version))
                .ToArray(),
            async (tenant, fields, cancellationToken) =>
            {
                var common = ParseCodeAndName(fields, requireEnglish: true);
                if (!common.Valid)
                {
                    return MasterDataImportParsedPayload.Rejected(fields, "import_row_invalid", common.Error!);
                }

                var currency = MasterDataImportFields.RequiredGuid(fields, "currencyid");
                if (currency.Error is not null)
                {
                    return MasterDataImportParsedPayload.Rejected(fields, "import_reference_invalid", currency.Error, "currencyId");
                }

                var currencyRecord = await currencies.FindCurrencyAsync(tenant, currency.Value, cancellationToken);
                if (currencyRecord is null || currencyRecord.LifecycleState != MasterDataLifecycleState.Active)
                {
                    return MasterDataImportParsedPayload.Quarantined(fields, "import_reference_invalid", "The Price List currency must be active in this Tenant.", "currencyId");
                }

                var customer = MasterDataImportFields.OptionalGuid(fields, "customerid");
                if (customer.Error is not null)
                {
                    return MasterDataImportParsedPayload.Rejected(fields, "import_field_invalid", customer.Error, "customerId");
                }

                CustomerRecord? customerRecord = null;
                if (customer.Value is { } customerId)
                {
                    customerRecord = await customers.FindCustomerAsync(tenant, customerId, cancellationToken);
                    if (customerRecord is null || customerRecord.LifecycleState != MasterDataLifecycleState.Active)
                    {
                        return MasterDataImportParsedPayload.Quarantined(fields, "import_reference_invalid", "The Price List customer must be active in this Tenant.", "customerId");
                    }
                }

                var scopeKindText = MasterDataImportFields.Optional(fields, "organizationscopekind");
                OrganizationScopeKind? scopeKindValue = null;
                string? scopeKindError = null;
                if (scopeKindText is not null)
                {
                    var scopeKind = MasterDataImportFields.OptionalEnum(fields, "organizationscopekind", OrganizationScopeKind.Company);
                    scopeKindValue = scopeKind.Value;
                    scopeKindError = scopeKind.Error;
                }
                var scopeId = MasterDataImportFields.OptionalGuid(fields, "organizationscopeid");
                var priority = MasterDataImportFields.OptionalInt(fields, "priority", 0);
                if (scopeKindError is not null || scopeId.Error is not null || priority.Error is not null)
                {
                    return MasterDataImportParsedPayload.Rejected(fields, "import_field_invalid", scopeKindError ?? scopeId.Error ?? priority.Error!);
                }

                var productId = MasterDataImportFields.OptionalGuid(fields, "productid");
                var unitId = MasterDataImportFields.OptionalGuid(fields, "unitofmeasureid");
                var effectiveFrom = MasterDataImportFields.OptionalDate(fields, "effectivefrom");
                var effectiveTo = MasterDataImportFields.OptionalDate(fields, "effectiveto");
                var price = MasterDataImportFields.OptionalDecimal(fields, "price");
                var priceScale = MasterDataImportFields.OptionalInt(fields, "pricescale", 2);
                var provenance = MasterDataImportFields.OptionalEnum(fields, "provenance", PriceListProvenance.Manual);
                if (productId.Error is not null || unitId.Error is not null || effectiveFrom.Error is not null
                    || effectiveTo.Error is not null || price.Error is not null || priceScale.Error is not null
                    || provenance.Error is not null)
                {
                    return MasterDataImportParsedPayload.Rejected(fields, "import_field_invalid", productId.Error ?? unitId.Error ?? effectiveFrom.Error ?? effectiveTo.Error ?? price.Error ?? priceScale.Error ?? provenance.Error!);
                }

                PriceListImportLine? line = null;
                if (productId.Value is { } product
                    || unitId.Value is { }
                    || effectiveFrom.Value is { }
                    || price.Value is { })
                {
                    if (productId.Value is not { } requiredProduct
                        || unitId.Value is not { } requiredUnit
                        || effectiveFrom.Value is not { } requiredFrom
                        || price.Value is not { } requiredPrice)
                    {
                        return MasterDataImportParsedPayload.Rejected(fields, "import_field_required", "A Price List line requires productId, unitOfMeasureId, effectiveFrom, and price.");
                    }

                    var productRecord = await products.FindProductAsync(tenant, requiredProduct, cancellationToken);
                    var unitRecord = await catalog.FindUnitOfMeasureAsync(tenant, requiredUnit, cancellationToken);
                    if (productRecord is null
                        || productRecord.LifecycleState != MasterDataLifecycleState.Active
                        || unitRecord is null
                        || unitRecord.LifecycleState != MasterDataLifecycleState.Active)
                    {
                        return MasterDataImportParsedPayload.Quarantined(fields, "import_reference_invalid", "The Price List line references active Product and UOM identities in this Tenant.");
                    }

                    var sourceReference = MasterDataPriceListValuePolicy.NormalizeSourceReference(MasterDataImportFields.Optional(fields, "sourcereference"));
                    line = new PriceListImportLine(
                        requiredProduct,
                        requiredUnit,
                        requiredFrom,
                        effectiveTo.Value,
                        requiredPrice,
                        priceScale.Value,
                        provenance.Value,
                        sourceReference);
                }

                try
                {
                    var code = MasterDataPriceListValuePolicy.NormalizeCode(common.Code);
                    MasterDataPriceListValuePolicy.ValidateApplicability(
                        currency.Value,
                        customer.Value,
                        scopeKindValue,
                        scopeId.Value,
                        priority.Value);
                    if (line is { } priceLine)
                    {
                        MasterDataPriceListValuePolicy.ValidatePrice(
                            priceLine.ProductId,
                            priceLine.UnitOfMeasureId,
                            priceLine.EffectiveFrom,
                            priceLine.EffectiveTo,
                            priceLine.Price,
                            priceLine.PriceScale,
                            priceLine.Provenance,
                            priceLine.SourceReference);
                    }

                    var command = new PriceListImportCommand(
                        new CreateMasterDataPriceListCommand(
                            code,
                            common.Name!,
                            currency.Value,
                            customer.Value,
                            scopeKindValue,
                            scopeId.Value,
                            priority.Value),
                        line);
                    return MasterDataImportParsedPayload.Success(fields, Identity("price-list", code), code, command);
                }
                catch (ArgumentException)
                {
                    return MasterDataImportParsedPayload.Rejected(fields, "import_field_invalid", "The Price List identity, applicability, or price is outside the approved bound.");
                }
            },
            async (context, row, parsed) =>
            {
                var importCommand = (PriceListImportCommand)parsed.Command!;
                var parent = importCommand.Parent;
                var evidence = Evidence(context, row, MasterDataResourceKind.PriceList, parent.Code);
                MasterDataPersistenceResult<MasterDataPriceListRecord> result;
                var expectedVersion = row.ExpectedResourceVersion;
                var isUpdate = expectedVersion is not null
                    && row.ResultingResourceId is { } existingId
                    && !existingId.Equals(MasterDataImportIdentity.DeterministicResourceId(context.Batch.Id, row));
                if (isUpdate)
                {
                    result = await persistence.EditPriceListAsync(
                        context.TenantContext,
                        new EditMasterDataPriceListCommand(
                            row.ResultingResourceId!.Value,
                            parent.Code,
                            parent.Name,
                            parent.CurrencyId,
                            parent.CustomerId,
                            parent.OrganizationScopeKind,
                            parent.OrganizationScopeId,
                            parent.Priority,
                            expectedVersion!),
                        evidence);
                }
                else
                {
                    result = await persistence.CreatePriceListAsync(
                        context.TenantContext,
                        row.ResultingResourceId ?? MasterDataImportIdentity.DeterministicResourceId(context.Batch.Id, row),
                        parent,
                        evidence);
                }

                if (!result.Succeeded)
                {
                    return Mutation(result, row, isUpdate);
                }

                if (importCommand.Line is null)
                {
                    return MasterDataImportMutationResult.Committed(row.ResultingResourceId, parent.Code, isUpdate);
                }

                var priceList = result.Value!;
                var line = importCommand.Line;
                var appended = await persistence.AppendPriceAsync(
                    context.TenantContext,
                    new AppendMasterDataPriceCommand(
                        priceList.Id,
                        line.ProductId,
                        line.UnitOfMeasureId,
                        line.EffectiveFrom,
                        line.EffectiveTo,
                        line.Price,
                        line.PriceScale,
                        line.Provenance,
                        line.SourceReference,
                        priceList.Version),
                    evidence);
                return Mutation(appended, row, isUpdate);
            },
            validateParsed: (context, parsed) =>
            {
                var command = (PriceListImportCommand)parsed.Command!;
                if (command.Parent.OrganizationScopeKind is null && command.Parent.OrganizationScopeId is null)
                {
                    return parsed;
                }

                var trustedScope = context.RequestContext.TrustedScope?.Value;
                var requestedScope = $"{command.Parent.OrganizationScopeKind}:{command.Parent.OrganizationScopeId:D}";
                return string.Equals(trustedScope, requestedScope, StringComparison.OrdinalIgnoreCase)
                    ? parsed
                    : MasterDataImportParsedPayload.Quarantined(
                        parsed.NormalizedFields,
                        "organization_scope_denied",
                        "The requested Price List organization applicability is outside the server-derived scope.");
            });

    private static MasterDataImportParsedPayload ParseSupplier(IReadOnlyDictionary<string, string?> fields)
    {
        var code = MasterDataImportFields.Required(fields, "code");
        var legal = ParseName(fields, requireEnglish: true, "legalnameenglish", "legalnamearabic");
        if (code.Error is not null)
        {
            return MasterDataImportParsedPayload.Rejected(fields, "import_field_required", code.Error, "code");
        }

        if (!legal.Valid)
        {
            return MasterDataImportParsedPayload.Rejected(fields, "import_row_invalid", legal.Error!);
        }

        var trading = ParseOptionalName(fields, "tradingnameenglish", "tradingnamearabic");
        if (trading.Error is not null)
        {
            return MasterDataImportParsedPayload.Rejected(fields, "import_field_invalid", trading.Error, "tradingNameEnglish");
        }

        var contacts = MasterDataImportFields.ParseContacts<SupplierContactCommand>(fields, "contacts");
        if (contacts.Error is not null)
        {
            return MasterDataImportParsedPayload.Rejected(fields, "import_field_invalid", contacts.Error, "contacts");
        }

        try
        {
            var normalizedCode = SupplierValuePolicy.NormalizeCode(code.Value!);
            SupplierValuePolicy.ValidateName(legal.Name!, nameof(CreateSupplierCommand.LegalName));
            var normalizedTrading = SupplierValuePolicy.NormalizeTradingName(trading.Name);
            var normalizedRegistration = SupplierValuePolicy.NormalizeRegistrationReference(MasterDataImportFields.Optional(fields, "registrationreference"));
            var normalizedContacts = SupplierValuePolicy.NormalizeContacts(contacts.Value!);
            var command = new CreateSupplierCommand(
                normalizedCode,
                legal.Name!,
                normalizedTrading,
                normalizedRegistration,
                normalizedContacts);
            return MasterDataImportParsedPayload.Success(fields, Identity("supplier", normalizedCode), normalizedCode, command);
        }
        catch (ArgumentException)
        {
            return MasterDataImportParsedPayload.Rejected(fields, "import_field_invalid", "The Supplier identity or mutable fields are outside the approved bound.");
        }
    }

    private static MasterDataImportParsedPayload ParseCustomer(IReadOnlyDictionary<string, string?> fields)
    {
        var code = MasterDataImportFields.Required(fields, "code");
        var legal = ParseName(fields, requireEnglish: true, "legalnameenglish", "legalnamearabic");
        if (code.Error is not null)
        {
            return MasterDataImportParsedPayload.Rejected(fields, "import_field_required", code.Error, "code");
        }

        if (!legal.Valid)
        {
            return MasterDataImportParsedPayload.Rejected(fields, "import_row_invalid", legal.Error!);
        }

        var trading = ParseOptionalName(fields, "tradingnameenglish", "tradingnamearabic");
        if (trading.Error is not null)
        {
            return MasterDataImportParsedPayload.Rejected(fields, "import_field_invalid", trading.Error, "tradingNameEnglish");
        }

        var contacts = MasterDataImportFields.ParseContacts<CustomerContactCommand>(fields, "contacts");
        if (contacts.Error is not null)
        {
            return MasterDataImportParsedPayload.Rejected(fields, "import_field_invalid", contacts.Error, "contacts");
        }

        try
        {
            var normalizedCode = CustomerValuePolicy.NormalizeCode(code.Value!);
            CustomerValuePolicy.ValidateName(legal.Name!, nameof(CreateCustomerCommand.LegalName));
            var normalizedTrading = CustomerValuePolicy.NormalizeTradingName(trading.Name);
            var normalizedContacts = CustomerValuePolicy.NormalizeContacts(contacts.Value!);
            var command = new CreateCustomerCommand(
                normalizedCode,
                legal.Name!,
                normalizedTrading,
                normalizedContacts);
            return MasterDataImportParsedPayload.Success(fields, Identity("customer", normalizedCode), normalizedCode, command);
        }
        catch (ArgumentException)
        {
            return MasterDataImportParsedPayload.Rejected(fields, "import_field_invalid", "The Business Customer identity or mutable fields are outside the approved bound.");
        }
    }

    private static MasterDataImportParsedPayload ParsePaymentTerm(IReadOnlyDictionary<string, string?> fields)
    {
        var common = ParseCodeAndName(fields, requireEnglish: false);
        if (!common.Valid)
        {
            return MasterDataImportParsedPayload.Rejected(fields, "import_row_invalid", common.Error!);
        }

        var effectiveFrom = MasterDataImportFields.RequiredDate(fields, "effectivefrom");
        var effectiveTo = MasterDataImportFields.OptionalDate(fields, "effectiveto");
        var baseDate = MasterDataImportFields.OptionalEnum(fields, "basedaterule", PaymentTermBaseDateRule.DocumentDate);
        var schedule = MasterDataImportFields.OptionalEnum(fields, "schedulemode", PaymentTermScheduleMode.SingleDueDate);
        var dueDays = MasterDataImportFields.OptionalInt(fields, "duedays", 0);
        var dueMonths = MasterDataImportFields.OptionalInt(fields, "duemonths", 0);
        var installments = MasterDataImportFields.ParseInstallments(fields, "installments");
        if (effectiveFrom.Error is not null || effectiveTo.Error is not null || baseDate.Error is not null
            || schedule.Error is not null || dueDays.Error is not null || dueMonths.Error is not null
            || installments.Error is not null)
        {
            return MasterDataImportParsedPayload.Rejected(fields, "import_field_invalid", effectiveFrom.Error ?? effectiveTo.Error ?? baseDate.Error ?? schedule.Error ?? dueDays.Error ?? dueMonths.Error ?? installments.Error!);
        }

        var early = MasterDataImportFields.ParseEarlyDiscount(fields);
        if (early.Error is not null)
        {
            return MasterDataImportParsedPayload.Rejected(fields, "import_field_invalid", early.Error, "earlySettlementDiscountPercentage");
        }

        try
        {
            var code = MasterDataCurrencyPaymentTermValuePolicy.NormalizeCode(common.Code);
            var command = new CreateMasterDataPaymentTermCommand(
                code,
                common.Name!,
                effectiveFrom.Value,
                effectiveTo.Value,
                baseDate.Value,
                schedule.Value,
                new MasterDataPaymentTermOffset(dueDays.Value, dueMonths.Value),
                installments.Value!,
                early.Value!);
            MasterDataCurrencyPaymentTermValuePolicy.ValidatePaymentTerm(
                command.EffectiveFrom,
                command.EffectiveTo,
                command.BaseDateRule,
                command.ScheduleMode,
                command.DueOffset,
                command.Installments,
                command.EarlySettlementDiscount);
            return MasterDataImportParsedPayload.Success(fields, Identity("payment-term", code), code, command);
        }
        catch (ArgumentException)
        {
            return MasterDataImportParsedPayload.Rejected(fields, "import_field_invalid", "The Payment Term identity or schedule is outside the approved bound.");
        }
    }

    private static MasterDataImportParsedPayload ParseTax(IReadOnlyDictionary<string, string?> fields)
    {
        var code = MasterDataImportFields.Required(fields, "code");
        var categoryCode = MasterDataImportFields.Required(fields, "categorycode");
        var category = ParseName(fields, requireEnglish: true, "categorynameenglish", "categorynamearabic");
        var name = ParseName(fields, requireEnglish: true);
        if (code.Error is not null || categoryCode.Error is not null)
        {
            return MasterDataImportParsedPayload.Rejected(fields, "import_field_required", code.Error ?? categoryCode.Error!);
        }

        if (!category.Valid)
        {
            return MasterDataImportParsedPayload.Rejected(fields, "import_row_invalid", category.Error!);
        }

        if (!name.Valid)
        {
            return MasterDataImportParsedPayload.Rejected(fields, "import_row_invalid", name.Error!);
        }

        var applicability = MasterDataImportFields.OptionalEnum(fields, "applicability", TaxDirection.Both);
        var effectiveFrom = MasterDataImportFields.RequiredDate(fields, "effectivefrom");
        var effectiveTo = MasterDataImportFields.OptionalDate(fields, "effectiveto");
        var rate = MasterDataImportFields.RequiredDecimal(fields, "ratepercentage");
        if (applicability.Error is not null || effectiveFrom.Error is not null || effectiveTo.Error is not null || rate.Error is not null)
        {
            return MasterDataImportParsedPayload.Rejected(fields, "import_field_invalid", applicability.Error ?? effectiveFrom.Error ?? effectiveTo.Error ?? rate.Error!);
        }

        try
        {
            var normalizedCode = MasterDataTaxValuePolicy.NormalizeCode(code.Value!);
            var normalizedCategoryCode = MasterDataTaxValuePolicy.NormalizeCategoryCode(categoryCode.Value!);
            var rateVersion = new MasterDataTaxRateVersion(effectiveFrom.Value, effectiveTo.Value, rate.Value);
            MasterDataTaxValuePolicy.ValidateRateVersion(rateVersion);
            var command = new CreateMasterDataTaxCommand(
                normalizedCode,
                normalizedCategoryCode,
                category.Name!,
                name.Name!,
                applicability.Value,
                rateVersion);
            return MasterDataImportParsedPayload.Success(fields, Identity("tax", normalizedCode), normalizedCode, command);
        }
        catch (ArgumentException)
        {
            return MasterDataImportParsedPayload.Rejected(fields, "import_field_invalid", "The Tax identity or rate version is outside the approved bound.");
        }
    }

    private static (bool Valid, string Code, LocalizedName? Name, string? Error) ParseCodeAndName(
        IReadOnlyDictionary<string, string?> fields,
        bool requireEnglish) => ParseCodeAndName(fields, requireEnglish, "englishname", "arabicname");

    private static (bool Valid, string Code, LocalizedName? Name, string? Error) ParseCodeAndName(
        IReadOnlyDictionary<string, string?> fields,
        bool requireEnglish,
        string englishKey,
        string arabicKey)
    {
        var code = MasterDataImportFields.Required(fields, "code");
        var name = ParseName(fields, requireEnglish, englishKey, arabicKey);
        return code.Error is not null
            ? (false, string.Empty, null, code.Error)
            : (name.Valid, code.Value!, name.Name, name.Error);
    }

    private static (bool Valid, LocalizedName? Name, string? Error) ParseName(
        IReadOnlyDictionary<string, string?> fields,
        bool requireEnglish,
        string englishKey = "englishname",
        string arabicKey = "arabicname")
    {
        var english = MasterDataImportFields.Optional(fields, englishKey);
        var arabic = MasterDataImportFields.Optional(fields, arabicKey);
        if (requireEnglish && string.IsNullOrWhiteSpace(english))
        {
            return (false, null, "An English localized name is required.");
        }

        if (string.IsNullOrWhiteSpace(english) && string.IsNullOrWhiteSpace(arabic))
        {
            return (false, null, "At least one localized name is required.");
        }

        try
        {
            return (true, new LocalizedName(english, arabic), null);
        }
        catch (ArgumentException exception)
        {
            return (false, null, exception.Message);
        }
    }

    private static (bool Valid, LocalizedName? Name, string? Error) ParseOptionalName(
        IReadOnlyDictionary<string, string?> fields,
        string englishKey,
        string arabicKey)
    {
        var english = MasterDataImportFields.Optional(fields, englishKey);
        var arabic = MasterDataImportFields.Optional(fields, arabicKey);
        if (string.IsNullOrWhiteSpace(english) && string.IsNullOrWhiteSpace(arabic))
        {
            return (true, null, null);
        }

        try
        {
            return (true, new LocalizedName(english, arabic), null);
        }
        catch (ArgumentException exception)
        {
            return (false, null, exception.Message);
        }
    }

    private static MasterDataAuditEvidence Evidence(
        MasterDataImportProcessingContext context,
        MasterDataImportRowRecord row,
        MasterDataResourceKind resourceKind,
        string code)
    {
        var resourceId = row.ResultingResourceId
            ?? MasterDataImportIdentity.DeterministicResourceId(context.Batch.Id, row);
        var resource = new MasterDataResourceReference(
            resourceKind,
            new TenantOwnership(context.Batch.TenantId.Value),
            resourceId,
            code,
            MasterDataImportScopePolicy.CreateScope(context.Batch.TenantId));
        return MasterDataAuditEvidenceFactory.Create(
            context.RequestContext,
            resource,
            MasterDataOperation.Import,
            MasterDataPolicyDecision.Allowed("import_row_allowed"),
            FoundationAuditReason.Allowed,
            afterSummary: $"import-batch:{context.Batch.Id:D};row:{row.OriginalRowNumber}");
    }

    private static MasterDataImportMutationResult Mutation<T>(
        MasterDataPersistenceResult<T> result,
        MasterDataImportRowRecord row,
        bool updated)
    {
        if (result.Succeeded)
        {
            return MasterDataImportMutationResult.Committed(
                row.ResultingResourceId,
                row.ResultingResourceCode,
                updated);
        }

        var code = result.Outcome switch
        {
            MasterDataPersistenceOutcome.Conflict => "import_concurrency_conflict",
            MasterDataPersistenceOutcome.Duplicate => "import_duplicate",
            MasterDataPersistenceOutcome.InvalidReference => "import_reference_invalid",
            MasterDataPersistenceOutcome.NotFound => "import_target_not_found",
            MasterDataPersistenceOutcome.AuditFailure => "import_audit_failed",
            _ => "import_commit_failed"
        };
        return MasterDataImportMutationResult.Failed(code, "The target persistence operation did not commit.");
    }

    private static string Identity(string resource, string value) =>
        $"{resource}:{value.Trim().ToUpperInvariant()}";
}

internal sealed record PriceListImportLine(
    Guid ProductId,
    Guid UnitOfMeasureId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal Price,
    int PriceScale,
    PriceListProvenance Provenance,
    string? SourceReference);

internal sealed record PriceListImportCommand(
    CreateMasterDataPriceListCommand Parent,
    PriceListImportLine? Line);

internal static class MasterDataImportIdentity
{
    internal static Guid DeterministicResourceId(Guid batchId, MasterDataImportRowRecord row) =>
        Create($"{batchId:D}|{row.ResourceKind}|{row.OriginalRowNumber}|{row.ReplaySequence}");

    private static Guid Create(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes[..16]);
    }
}

internal static class MasterDataImportFields
{
    internal const int MaximumFieldCount = 256;
    internal const int MaximumFieldValueLength = 16384;

    internal static IReadOnlyDictionary<string, string?> Normalize(
        IReadOnlyDictionary<string, string?> fields,
        out (string Code, string Message, string? Field)? failure)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        failure = null;
        if (fields is null)
        {
            failure = ("import_row_invalid", "A row field map is required.", null);
            return result;
        }

        if (fields.Count > MaximumFieldCount)
        {
            failure = ("import_row_payload_too_large", "The row contains more fields than the approved technical bound.", null);
            return result;
        }

        foreach (var pair in fields)
        {
            var key = pair.Key?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(key) || key.Length > 128 || key.Any(char.IsControl) || key.StartsWith('_'))
            {
                failure = ("import_field_invalid", "A row field name is outside the approved boundary.", pair.Key);
                return result;
            }

            if (key is "tenantid" or "tenant_id" or "tenant" or "actorid" or "permission" or "scope")
            {
                failure = ("import_untrusted_field", "Tenant and authority fields are server-derived and cannot be imported.", pair.Key);
                return result;
            }

            var normalizedValue = NormalizeValue(pair.Value);
            if (normalizedValue?.Length > MaximumFieldValueLength)
            {
                failure = ("import_field_too_large", "A row field value is outside the approved technical bound.", pair.Key);
                return result;
            }

            if (!result.TryAdd(key, normalizedValue))
            {
                failure = ("import_field_duplicate", "The row contains the same field more than once after normalization.", pair.Key);
                return result;
            }
        }

        return result;
    }

    internal static IReadOnlyDictionary<string, string?> WithIdentity(
        IReadOnlyDictionary<string, string?> fields,
        string identity)
    {
        var result = new Dictionary<string, string?>(fields, StringComparer.OrdinalIgnoreCase)
        {
            ["_identityKey"] = identity
        };
        return result;
    }

    internal static string? Optional(IReadOnlyDictionary<string, string?> fields, string key) =>
        fields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    internal static (string? Value, string? Error) Required(
        IReadOnlyDictionary<string, string?> fields,
        string key)
    {
        var value = Optional(fields, key);
        return value is null ? (null, $"The '{key}' field is required.") : (value, null);
    }

    internal static (Guid Value, string? Error) RequiredGuid(
        IReadOnlyDictionary<string, string?> fields,
        string key)
    {
        var value = Required(fields, key);
        if (value.Error is not null || !Guid.TryParse(value.Value, out var parsed) || parsed == Guid.Empty)
        {
            return (Guid.Empty, value.Error ?? $"The '{key}' field must be a non-empty GUID.");
        }

        return (parsed, null);
    }

    internal static (Guid? Value, string? Error) OptionalGuid(
        IReadOnlyDictionary<string, string?> fields,
        string key)
    {
        var value = Optional(fields, key);
        if (value is null)
        {
            return (null, null);
        }

        return Guid.TryParse(value, out var parsed) && parsed != Guid.Empty
            ? (parsed, null)
            : (null, $"The '{key}' field must be a non-empty GUID when supplied.");
    }

    internal static (DateOnly Value, string? Error) RequiredDate(
        IReadOnlyDictionary<string, string?> fields,
        string key)
    {
        var value = Required(fields, key);
        return value.Error is null && DateOnly.TryParse(value.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? (parsed, null)
            : (default, value.Error ?? $"The '{key}' field must be an ISO date.");
    }

    internal static (DateOnly? Value, string? Error) OptionalDate(
        IReadOnlyDictionary<string, string?> fields,
        string key)
    {
        var value = Optional(fields, key);
        if (value is null)
        {
            return (null, null);
        }

        return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? (parsed, null)
            : (null, $"The '{key}' field must be an ISO date when supplied.");
    }

    internal static (decimal Value, string? Error) RequiredDecimal(
        IReadOnlyDictionary<string, string?> fields,
        string key)
    {
        var value = Required(fields, key);
        return value.Error is null && decimal.TryParse(value.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? (parsed, null)
            : (default, value.Error ?? $"The '{key}' field must be a decimal number.");
    }

    internal static (decimal? Value, string? Error) OptionalDecimal(
        IReadOnlyDictionary<string, string?> fields,
        string key)
    {
        var value = Optional(fields, key);
        if (value is null)
        {
            return (null, null);
        }

        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? (parsed, null)
            : (null, $"The '{key}' field must be a decimal number when supplied.");
    }

    internal static (int Value, string? Error) OptionalInt(
        IReadOnlyDictionary<string, string?> fields,
        string key,
        int defaultValue)
    {
        var value = Optional(fields, key);
        if (value is null)
        {
            return (defaultValue, null);
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? (parsed, null)
            : (defaultValue, $"The '{key}' field must be an integer when supplied.");
    }

    internal static (bool Value, string? Error) OptionalBool(
        IReadOnlyDictionary<string, string?> fields,
        string key,
        bool defaultValue)
    {
        var value = Optional(fields, key);
        if (value is null)
        {
            return (defaultValue, null);
        }

        if (bool.TryParse(value, out var parsed))
        {
            return (parsed, null);
        }

        return value switch
        {
            "1" => (true, null),
            "0" => (false, null),
            _ => (defaultValue, $"The '{key}' field must be boolean when supplied.")
        };
    }

    internal static (bool? Value, string? Error) OptionalNullableBool(
        IReadOnlyDictionary<string, string?> fields,
        string key)
    {
        var value = Optional(fields, key);
        if (value is null)
        {
            return (null, null);
        }

        var parsed = OptionalBool(fields, key, false);
        return parsed.Error is null ? (parsed.Value, null) : (null, parsed.Error);
    }

    internal static (T Value, string? Error) OptionalEnum<T>(
        IReadOnlyDictionary<string, string?> fields,
        string key,
        T defaultValue)
        where T : struct, Enum
    {
        var value = Optional(fields, key);
        if (value is null)
        {
            return (defaultValue, null);
        }

        if (Enum.TryParse<T>(value, true, out var parsed) && Enum.IsDefined(parsed))
        {
            return (parsed, null);
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric)
            && Enum.IsDefined(typeof(T), numeric))
        {
            return ((T)Enum.ToObject(typeof(T), numeric), null);
        }

        return (defaultValue, $"The '{key}' field is not a supported value.");
    }

    internal static (IReadOnlyList<string>? Value, string? Error) OptionalStringList(
        IReadOnlyDictionary<string, string?> fields,
        string key)
    {
        var value = Optional(fields, key);
        if (value is null)
        {
            return (null, null);
        }

        try
        {
            var values = value.StartsWith('[')
                ? JsonSerializer.Deserialize<string[]>(value) ?? []
                : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var normalized = values.Select(item => item.Trim()).Where(item => item.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            return (normalized, null);
        }
        catch
        {
            return (null, $"The '{key}' field must be a comma-separated list or JSON string array.");
        }
    }

    internal static (IReadOnlyList<T>? Value, string? Error) ParseContacts<T>(
        IReadOnlyDictionary<string, string?> fields,
        string key)
    {
        var value = Optional(fields, key);
        if (value is null)
        {
            return ([], null);
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return (null, "Contacts must be a JSON array.");
            }

            var result = new List<T>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                var name = element.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                var email = element.TryGetProperty("email", out var emailElement) ? emailElement.GetString() : null;
                var phone = element.TryGetProperty("phone", out var phoneElement) ? phoneElement.GetString() : null;
                if (string.IsNullOrWhiteSpace(name))
                {
                    return (null, "Every contact requires a name.");
                }

                object contact = typeof(T) == typeof(SupplierContactCommand)
                    ? new SupplierContactCommand(name.Trim(), email?.Trim(), phone?.Trim())
                    : new CustomerContactCommand(name.Trim(), email?.Trim(), phone?.Trim());
                result.Add((T)contact);
            }

            return (result, null);
        }
        catch
        {
            return (null, "Contacts must be a valid JSON array.");
        }
    }

    internal static (IReadOnlyList<MasterDataPaymentTermInstallment>? Value, string? Error) ParseInstallments(
        IReadOnlyDictionary<string, string?> fields,
        string key)
    {
        var value = Optional(fields, key);
        if (value is null)
        {
            return ([], null);
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return (null, "Installments must be a JSON array.");
            }

            var result = new List<MasterDataPaymentTermInstallment>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                var sequence = element.TryGetProperty("sequence", out var sequenceElement) ? sequenceElement.GetInt32() : 0;
                var percentage = element.TryGetProperty("percentage", out var percentageElement) ? percentageElement.GetDecimal() : 0;
                var days = element.TryGetProperty("days", out var daysElement) ? daysElement.GetInt32() : 0;
                var months = element.TryGetProperty("months", out var monthsElement) ? monthsElement.GetInt32() : 0;
                if (sequence < 1 || percentage <= 0)
                {
                    return (null, "Every installment requires a positive sequence and percentage.");
                }

                result.Add(new MasterDataPaymentTermInstallment(sequence, percentage, new MasterDataPaymentTermOffset(days, months)));
            }

            return (result, null);
        }
        catch
        {
            return (null, "Installments must be a valid JSON array.");
        }
    }

    internal static (MasterDataEarlySettlementDiscount? Value, string? Error) ParseEarlyDiscount(
        IReadOnlyDictionary<string, string?> fields)
    {
        var enabled = OptionalBool(fields, "earlysettlementdiscountenabled", false);
        var percentage = OptionalDecimal(fields, "earlysettlementdiscountpercentage");
        var days = OptionalInt(fields, "earlysettlementdiscountdays", 0);
        var months = OptionalInt(fields, "earlysettlementdiscountmonths", 0);
        if (enabled.Error is not null || percentage.Error is not null || days.Error is not null || months.Error is not null)
        {
            return (null, enabled.Error ?? percentage.Error ?? days.Error ?? months.Error!);
        }

        return (new MasterDataEarlySettlementDiscount(
            enabled.Value,
            percentage.Value,
            new MasterDataPaymentTermOffset(days.Value, months.Value)), null);
    }

    private static string? NormalizeValue(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length == 0 ? null : normalized;
    }
}

#pragma warning restore CS1591
