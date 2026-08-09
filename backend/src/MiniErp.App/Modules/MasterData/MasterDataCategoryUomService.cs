#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.MasterData;

public sealed record MasterDataOperationResult<T>(
    bool Succeeded,
    string Code,
    T? Value,
    MasterDataAuditEvidence? Evidence)
{
    public static MasterDataOperationResult<T> Success(
        T value,
        MasterDataAuditEvidence? evidence = null) =>
        new(true, "succeeded", value, evidence);

    public static MasterDataOperationResult<T> Failure(
        string code,
        MasterDataAuditEvidence? evidence = null) =>
        new(false, code, default, evidence);
}

/// <summary>
/// Application behavior for the bounded M95-SL-02 Category/UOM slice.
/// Every operation receives a trusted server-derived context; the command
/// payload never supplies Tenant or business-scope authority.
/// </summary>
public sealed class MasterDataCategoryUomService
{
    private readonly MasterDataResourceAuthorizationService authorization;
    private readonly IMasterDataCatalogPersistence persistence;
    private readonly MasterDataCategoryHierarchyPolicy hierarchyPolicy;

    public MasterDataCategoryUomService(
        MasterDataResourceAuthorizationService authorization,
        IMasterDataCatalogPersistence persistence,
        MasterDataCategoryHierarchyPolicy? hierarchyPolicy = null)
    {
        this.authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        this.hierarchyPolicy = hierarchyPolicy ?? new MasterDataCategoryHierarchyPolicy();
    }

    public async Task<MasterDataOperationResult<IReadOnlyList<MasterDataCategoryRecord>>> ListCategoriesAsync(
        MasterDataRequestContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var resource = Resource(
            MasterDataResourceKind.ProductCategory,
            context,
            stableId: null,
            businessCode: "category-list");
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.View);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<IReadOnlyList<MasterDataCategoryRecord>>(
                context,
                resource,
                MasterDataOperation.View,
                authorized.Decision,
                authorized.Code,
                cancellationToken);
        }

        try
        {
            var records = await persistence.ListCategoriesAsync(
                context.TenantContext,
                cancellationToken);
            return MasterDataOperationResult<IReadOnlyList<MasterDataCategoryRecord>>.Success(records);
        }
        catch
        {
            return await FailedAsync<IReadOnlyList<MasterDataCategoryRecord>>(
                context,
                resource,
                MasterDataOperation.View,
                "persistence_unavailable",
                cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<MasterDataCategoryRecord>> GetCategoryAsync(
        MasterDataRequestContext context,
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var resource = Resource(
            MasterDataResourceKind.ProductCategory,
            context,
            categoryId,
            businessCode: null);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.View);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataCategoryRecord>(
                context,
                resource,
                MasterDataOperation.View,
                authorized.Decision,
                authorized.Code,
                cancellationToken);
        }

        try
        {
            var record = await persistence.FindCategoryAsync(
                context.TenantContext,
                categoryId,
                cancellationToken);
            if (record is null)
            {
                return await FailedAsync<MasterDataCategoryRecord>(
                    context,
                    resource,
                    MasterDataOperation.View,
                    "category_not_found",
                    cancellationToken);
            }

            return MasterDataOperationResult<MasterDataCategoryRecord>.Success(record);
        }
        catch
        {
            return await FailedAsync<MasterDataCategoryRecord>(
                context,
                resource,
                MasterDataOperation.View,
                "persistence_unavailable",
                cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<MasterDataCategoryRecord>> CreateCategoryAsync(
        MasterDataRequestContext context,
        CreateMasterDataCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);

        string code;
        try
        {
            code = MasterDataCategoryUomValuePolicy.NormalizeCode(command.Code);
            ArgumentNullException.ThrowIfNull(command.Name);
        }
        catch (ArgumentException)
        {
            return MasterDataOperationResult<MasterDataCategoryRecord>.Failure("validation_failed");
        }

        var categoryId = Guid.NewGuid();
        var resource = Resource(
            MasterDataResourceKind.ProductCategory,
            context,
            categoryId,
            code);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.Create);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataCategoryRecord>(
                context,
                resource,
                MasterDataOperation.Create,
                authorized.Decision,
                authorized.Code,
                cancellationToken);
        }

        MasterDataHierarchyValidationResult validation;
        try
        {
            validation = await ValidateCategoryWriteAsync(
                context.TenantContext,
                categoryId,
                code,
                command.Name,
                command.ParentCategoryId,
                cancellationToken);
        }
        catch
        {
            return await FailedAsync<MasterDataCategoryRecord>(
                context,
                resource,
                MasterDataOperation.Create,
                "persistence_unavailable",
                cancellationToken);
        }
        if (!validation.Valid)
        {
            return await FailedAsync<MasterDataCategoryRecord>(
                context,
                resource,
                MasterDataOperation.Create,
                validation.Code,
                cancellationToken);
        }

        var evidence = CreateEvidence(
            context,
            resource,
            MasterDataOperation.Create,
            authorized.Decision,
            FoundationAuditReason.Allowed,
            beforeSummary: null,
            afterSummary: CategorySummary(code, command.Name, MasterDataLifecycleState.Active));

        try
        {
            var result = await persistence.CreateCategoryAsync(
                context.TenantContext,
                categoryId,
                command with { Code = code },
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
            return await FailedAsync<MasterDataCategoryRecord>(
                context,
                resource,
                MasterDataOperation.Create,
                "persistence_unavailable",
                cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<MasterDataCategoryRecord>> EditCategoryAsync(
        MasterDataRequestContext context,
        EditMasterDataCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);

        string code;
        try
        {
            code = MasterDataCategoryUomValuePolicy.NormalizeCode(command.Code);
            ArgumentNullException.ThrowIfNull(command.Name);
            ValidateVersion(command.ExpectedVersion);
        }
        catch (ArgumentException)
        {
            return MasterDataOperationResult<MasterDataCategoryRecord>.Failure("validation_failed");
        }

        var resource = Resource(
            MasterDataResourceKind.ProductCategory,
            context,
            command.CategoryId,
            code);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.Edit);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataCategoryRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                authorized.Decision,
                authorized.Code,
                cancellationToken);
        }

        MasterDataCategoryRecord? current;
        try
        {
            current = await persistence.FindCategoryAsync(
                context.TenantContext,
                command.CategoryId,
                cancellationToken);
        }
        catch
        {
            return await FailedAsync<MasterDataCategoryRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                "persistence_unavailable",
                cancellationToken);
        }

        if (current is null)
        {
            return await FailedAsync<MasterDataCategoryRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                "category_not_found",
                cancellationToken);
        }

        MasterDataHierarchyValidationResult validation;
        try
        {
            validation = await ValidateCategoryWriteAsync(
                context.TenantContext,
                command.CategoryId,
                code,
                command.Name,
                command.ParentCategoryId,
                cancellationToken);
        }
        catch
        {
            return await FailedAsync<MasterDataCategoryRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                "persistence_unavailable",
                cancellationToken);
        }
        if (!validation.Valid)
        {
            return await FailedAsync<MasterDataCategoryRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                validation.Code,
                cancellationToken);
        }

        var evidence = CreateEvidence(
            context,
            resource,
            MasterDataOperation.Edit,
            authorized.Decision,
            FoundationAuditReason.Allowed,
            CategorySummary(current.Code, current.Name, current.LifecycleState),
            CategorySummary(code, command.Name, current.LifecycleState));

        try
        {
            var result = await persistence.EditCategoryAsync(
                context.TenantContext,
                command with { Code = code },
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
            return await FailedAsync<MasterDataCategoryRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                "persistence_unavailable",
                cancellationToken);
        }
    }

    public Task<MasterDataOperationResult<MasterDataCategoryRecord>> ActivateCategoryAsync(
        MasterDataRequestContext context,
        Guid categoryId,
        byte[] expectedVersion,
        CancellationToken cancellationToken = default) =>
        SetCategoryLifecycleAsync(
            context,
            categoryId,
            MasterDataLifecycleState.Active,
            expectedVersion,
            cancellationToken);

    public Task<MasterDataOperationResult<MasterDataCategoryRecord>> DeactivateCategoryAsync(
        MasterDataRequestContext context,
        Guid categoryId,
        byte[] expectedVersion,
        CancellationToken cancellationToken = default) =>
        SetCategoryLifecycleAsync(
            context,
            categoryId,
            MasterDataLifecycleState.Inactive,
            expectedVersion,
            cancellationToken);

    public Task<MasterDataOperationResult<MasterDataCategoryRecord>> ReactivateCategoryAsync(
        MasterDataRequestContext context,
        Guid categoryId,
        byte[] expectedVersion,
        CancellationToken cancellationToken = default) =>
        SetCategoryLifecycleAsync(
            context,
            categoryId,
            MasterDataLifecycleState.Active,
            expectedVersion,
            cancellationToken,
            forceReactivation: true);

    public async Task<MasterDataOperationResult<MasterDataCategoryRecord>> SetCategoryLifecycleAsync(
        MasterDataRequestContext context,
        Guid categoryId,
        MasterDataLifecycleState lifecycleState,
        byte[] expectedVersion,
        CancellationToken cancellationToken = default,
        bool forceReactivation = false)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            ValidateVersion(expectedVersion);
            if (!Enum.IsDefined(lifecycleState))
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleState));
            }
        }
        catch (ArgumentException)
        {
            return MasterDataOperationResult<MasterDataCategoryRecord>.Failure("validation_failed");
        }

        var resource = Resource(
            MasterDataResourceKind.ProductCategory,
            context,
            categoryId,
            businessCode: null);
        var operation = lifecycleState == MasterDataLifecycleState.Active
            ? (forceReactivation ? MasterDataOperation.Reactivate : MasterDataOperation.Activate)
            : MasterDataOperation.Deactivate;
        var authorized = authorization.Authorize(context, resource, operation);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataCategoryRecord>(
                context,
                resource,
                operation,
                authorized.Decision,
                authorized.Code,
                cancellationToken);
        }

        MasterDataCategoryRecord? current;
        try
        {
            current = await persistence.FindCategoryAsync(context.TenantContext, categoryId, cancellationToken);
        }
        catch
        {
            return await FailedAsync<MasterDataCategoryRecord>(
                context,
                resource,
                operation,
                "persistence_unavailable",
                cancellationToken);
        }

        if (current is null)
        {
            return await FailedAsync<MasterDataCategoryRecord>(
                context,
                resource,
                operation,
                "category_not_found",
                cancellationToken);
        }

        resource = Resource(
            MasterDataResourceKind.ProductCategory,
            context,
            categoryId,
            current.Code);

        var evidence = CreateEvidence(
            context,
            resource,
            operation,
            authorized.Decision,
            FoundationAuditReason.Allowed,
            CategorySummary(current.Code, current.Name, current.LifecycleState),
            CategorySummary(current.Code, current.Name, lifecycleState));

        if (current.LifecycleState == lifecycleState)
        {
            var audit = await persistence.AppendAuditAsync(
                context.TenantContext,
                evidence,
                cancellationToken);
            return audit.Succeeded
                ? MasterDataOperationResult<MasterDataCategoryRecord>.Success(current, evidence)
                : MasterDataOperationResult<MasterDataCategoryRecord>.Failure("audit_unavailable", evidence);
        }

        try
        {
            var result = await persistence.SetCategoryLifecycleAsync(
                context.TenantContext,
                categoryId,
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
            return await FailedAsync<MasterDataCategoryRecord>(
                context,
                resource,
                operation,
                "persistence_unavailable",
                cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<IReadOnlyList<MasterDataUnitOfMeasureRecord>>> ListUnitsOfMeasureAsync(
        MasterDataRequestContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var resource = Resource(
            MasterDataResourceKind.UnitOfMeasure,
            context,
            stableId: null,
            businessCode: "uom-list");
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.View);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<IReadOnlyList<MasterDataUnitOfMeasureRecord>>(
                context,
                resource,
                MasterDataOperation.View,
                authorized.Decision,
                authorized.Code,
                cancellationToken);
        }

        try
        {
            return MasterDataOperationResult<IReadOnlyList<MasterDataUnitOfMeasureRecord>>.Success(
                await persistence.ListUnitsOfMeasureAsync(context.TenantContext, cancellationToken));
        }
        catch
        {
            return await FailedAsync<IReadOnlyList<MasterDataUnitOfMeasureRecord>>(
                context,
                resource,
                MasterDataOperation.View,
                "persistence_unavailable",
                cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<MasterDataUnitOfMeasureRecord>> GetUnitOfMeasureAsync(
        MasterDataRequestContext context,
        Guid unitOfMeasureId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var resource = Resource(
            MasterDataResourceKind.UnitOfMeasure,
            context,
            unitOfMeasureId,
            businessCode: null);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.View);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataUnitOfMeasureRecord>(
                context,
                resource,
                MasterDataOperation.View,
                authorized.Decision,
                authorized.Code,
                cancellationToken);
        }

        try
        {
            var record = await persistence.FindUnitOfMeasureAsync(
                context.TenantContext,
                unitOfMeasureId,
                cancellationToken);
            return record is null
                ? await FailedAsync<MasterDataUnitOfMeasureRecord>(
                    context,
                    resource,
                    MasterDataOperation.View,
                    "uom_not_found",
                    cancellationToken)
                : MasterDataOperationResult<MasterDataUnitOfMeasureRecord>.Success(record);
        }
        catch
        {
            return await FailedAsync<MasterDataUnitOfMeasureRecord>(
                context,
                resource,
                MasterDataOperation.View,
                "persistence_unavailable",
                cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<MasterDataUnitOfMeasureRecord>> CreateUnitOfMeasureAsync(
        MasterDataRequestContext context,
        CreateMasterDataUnitOfMeasureCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);

        string code;
        try
        {
            code = MasterDataCategoryUomValuePolicy.NormalizeCode(command.Code);
            ArgumentNullException.ThrowIfNull(command.Name);
        }
        catch (ArgumentException)
        {
            return MasterDataOperationResult<MasterDataUnitOfMeasureRecord>.Failure("validation_failed");
        }

        var unitId = Guid.NewGuid();
        var resource = Resource(MasterDataResourceKind.UnitOfMeasure, context, unitId, code);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.Create);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataUnitOfMeasureRecord>(
                context,
                resource,
                MasterDataOperation.Create,
                authorized.Decision,
                authorized.Code,
                cancellationToken);
        }

        MasterDataHierarchyValidationResult validation;
        try
        {
            validation = await ValidateUnitNameAsync(
                context.TenantContext,
                code,
                command.Name,
                cancellationToken);
        }
        catch
        {
            return await FailedAsync<MasterDataUnitOfMeasureRecord>(
                context,
                resource,
                MasterDataOperation.Create,
                "persistence_unavailable",
                cancellationToken);
        }
        if (!validation.Valid)
        {
            return await FailedAsync<MasterDataUnitOfMeasureRecord>(
                context,
                resource,
                MasterDataOperation.Create,
                validation.Code,
                cancellationToken);
        }

        var evidence = CreateEvidence(
            context,
            resource,
            MasterDataOperation.Create,
            authorized.Decision,
            FoundationAuditReason.Allowed,
            beforeSummary: null,
            afterSummary: UomSummary(code, command.Name, MasterDataLifecycleState.Active));

        try
        {
            var result = await persistence.CreateUnitOfMeasureAsync(
                context.TenantContext,
                unitId,
                command with { Code = code },
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
            return await FailedAsync<MasterDataUnitOfMeasureRecord>(
                context,
                resource,
                MasterDataOperation.Create,
                "persistence_unavailable",
                cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<MasterDataUnitOfMeasureRecord>> EditUnitOfMeasureAsync(
        MasterDataRequestContext context,
        EditMasterDataUnitOfMeasureCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);

        string code;
        try
        {
            code = MasterDataCategoryUomValuePolicy.NormalizeCode(command.Code);
            ArgumentNullException.ThrowIfNull(command.Name);
            ValidateVersion(command.ExpectedVersion);
        }
        catch (ArgumentException)
        {
            return MasterDataOperationResult<MasterDataUnitOfMeasureRecord>.Failure("validation_failed");
        }

        var resource = Resource(MasterDataResourceKind.UnitOfMeasure, context, command.UnitOfMeasureId, code);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.Edit);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataUnitOfMeasureRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                authorized.Decision,
                authorized.Code,
                cancellationToken);
        }

        MasterDataUnitOfMeasureRecord? current;
        try
        {
            current = await persistence.FindUnitOfMeasureAsync(
                context.TenantContext,
                command.UnitOfMeasureId,
                cancellationToken);
        }
        catch
        {
            return await FailedAsync<MasterDataUnitOfMeasureRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                "persistence_unavailable",
                cancellationToken);
        }

        if (current is null)
        {
            return await FailedAsync<MasterDataUnitOfMeasureRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                "uom_not_found",
                cancellationToken);
        }

        MasterDataHierarchyValidationResult validation;
        try
        {
            validation = await ValidateUnitNameAsync(
                context.TenantContext,
                code,
                command.Name,
                cancellationToken,
                command.UnitOfMeasureId);
        }
        catch
        {
            return await FailedAsync<MasterDataUnitOfMeasureRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                "persistence_unavailable",
                cancellationToken);
        }
        if (!validation.Valid)
        {
            return await FailedAsync<MasterDataUnitOfMeasureRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                validation.Code,
                cancellationToken);
        }

        var evidence = CreateEvidence(
            context,
            resource,
            MasterDataOperation.Edit,
            authorized.Decision,
            FoundationAuditReason.Allowed,
            UomSummary(current.Code, current.Name, current.LifecycleState),
            UomSummary(code, command.Name, current.LifecycleState));

        try
        {
            var result = await persistence.EditUnitOfMeasureAsync(
                context.TenantContext,
                command with { Code = code },
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
            return await FailedAsync<MasterDataUnitOfMeasureRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                "persistence_unavailable",
                cancellationToken);
        }
    }

    public Task<MasterDataOperationResult<MasterDataUnitOfMeasureRecord>> ActivateUnitOfMeasureAsync(
        MasterDataRequestContext context,
        Guid unitOfMeasureId,
        byte[] expectedVersion,
        CancellationToken cancellationToken = default) =>
        SetUnitLifecycleAsync(context, unitOfMeasureId, MasterDataLifecycleState.Active, expectedVersion, cancellationToken);

    public Task<MasterDataOperationResult<MasterDataUnitOfMeasureRecord>> DeactivateUnitOfMeasureAsync(
        MasterDataRequestContext context,
        Guid unitOfMeasureId,
        byte[] expectedVersion,
        CancellationToken cancellationToken = default) =>
        SetUnitLifecycleAsync(context, unitOfMeasureId, MasterDataLifecycleState.Inactive, expectedVersion, cancellationToken);

    public Task<MasterDataOperationResult<MasterDataUnitOfMeasureRecord>> ReactivateUnitOfMeasureAsync(
        MasterDataRequestContext context,
        Guid unitOfMeasureId,
        byte[] expectedVersion,
        CancellationToken cancellationToken = default) =>
        SetUnitLifecycleAsync(context, unitOfMeasureId, MasterDataLifecycleState.Active, expectedVersion, cancellationToken, true);

    public async Task<MasterDataOperationResult<MasterDataUnitOfMeasureRecord>> SetUnitLifecycleAsync(
        MasterDataRequestContext context,
        Guid unitOfMeasureId,
        MasterDataLifecycleState lifecycleState,
        byte[] expectedVersion,
        CancellationToken cancellationToken = default,
        bool forceReactivation = false)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            ValidateVersion(expectedVersion);
            if (!Enum.IsDefined(lifecycleState))
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleState));
            }
        }
        catch (ArgumentException)
        {
            return MasterDataOperationResult<MasterDataUnitOfMeasureRecord>.Failure("validation_failed");
        }

        var resource = Resource(MasterDataResourceKind.UnitOfMeasure, context, unitOfMeasureId, null);
        var operation = lifecycleState == MasterDataLifecycleState.Active
            ? (forceReactivation ? MasterDataOperation.Reactivate : MasterDataOperation.Activate)
            : MasterDataOperation.Deactivate;
        var authorized = authorization.Authorize(context, resource, operation);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataUnitOfMeasureRecord>(
                context,
                resource,
                operation,
                authorized.Decision,
                authorized.Code,
                cancellationToken);
        }

        MasterDataUnitOfMeasureRecord? current;
        try
        {
            current = await persistence.FindUnitOfMeasureAsync(context.TenantContext, unitOfMeasureId, cancellationToken);
        }
        catch
        {
            return await FailedAsync<MasterDataUnitOfMeasureRecord>(
                context,
                resource,
                operation,
                "persistence_unavailable",
                cancellationToken);
        }

        if (current is null)
        {
            return await FailedAsync<MasterDataUnitOfMeasureRecord>(
                context,
                resource,
                operation,
                "uom_not_found",
                cancellationToken);
        }

        resource = Resource(MasterDataResourceKind.UnitOfMeasure, context, unitOfMeasureId, current.Code);
        var evidence = CreateEvidence(
            context,
            resource,
            operation,
            authorized.Decision,
            FoundationAuditReason.Allowed,
            UomSummary(current.Code, current.Name, current.LifecycleState),
            UomSummary(current.Code, current.Name, lifecycleState));

        if (current.LifecycleState == lifecycleState)
        {
            var audit = await persistence.AppendAuditAsync(context.TenantContext, evidence, cancellationToken);
            return audit.Succeeded
                ? MasterDataOperationResult<MasterDataUnitOfMeasureRecord>.Success(current, evidence)
                : MasterDataOperationResult<MasterDataUnitOfMeasureRecord>.Failure("audit_unavailable", evidence);
        }

        try
        {
            var result = await persistence.SetUnitOfMeasureLifecycleAsync(
                context.TenantContext,
                unitOfMeasureId,
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
            return await FailedAsync<MasterDataUnitOfMeasureRecord>(
                context,
                resource,
                operation,
                "persistence_unavailable",
                cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<MasterDataConversionRecord>> CreateConversionAsync(
        MasterDataRequestContext context,
        CreateMasterDataConversionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);

        var conversionId = Guid.NewGuid();
        var resource = Resource(MasterDataResourceKind.UnitOfMeasure, context, conversionId, "conversion");
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.Edit);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataConversionRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                authorized.Decision,
                authorized.Code,
                cancellationToken);
        }

        try
        {
            if (command.FromUnitOfMeasureId == Guid.Empty
                || command.ToUnitOfMeasureId == Guid.Empty
                || command.FromUnitOfMeasureId == command.ToUnitOfMeasureId)
            {
                return await FailedAsync<MasterDataConversionRecord>(
                    context,
                    resource,
                    MasterDataOperation.Edit,
                    "conversion_reference_invalid",
                    cancellationToken);
            }

            MasterDataCategoryUomValuePolicy.ValidateConversionFactor(command.Factor);
            var units = await persistence.ListUnitsOfMeasureAsync(context.TenantContext, cancellationToken);
            var from = units.SingleOrDefault(unit => unit.Id == command.FromUnitOfMeasureId);
            var to = units.SingleOrDefault(unit => unit.Id == command.ToUnitOfMeasureId);
            if (from is null || to is null)
            {
                return await FailedAsync<MasterDataConversionRecord>(
                    context,
                    resource,
                    MasterDataOperation.Edit,
                    "conversion_reference_invalid",
                    cancellationToken);
            }

            if (from.LifecycleState != MasterDataLifecycleState.Active
                || to.LifecycleState != MasterDataLifecycleState.Active)
            {
                return await FailedAsync<MasterDataConversionRecord>(
                    context,
                    resource,
                    MasterDataOperation.Edit,
                    "conversion_reference_inactive",
                    cancellationToken);
            }

            var evidence = CreateEvidence(
                context,
                resource,
                MasterDataOperation.Edit,
                authorized.Decision,
                FoundationAuditReason.Allowed,
                beforeSummary: null,
                afterSummary: $"from={from.Code};to={to.Code};factor={command.Factor.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            var result = await persistence.CreateConversionAsync(
                context.TenantContext,
                conversionId,
                command,
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
        catch (ArgumentException)
        {
            return await FailedAsync<MasterDataConversionRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                "validation_failed",
                cancellationToken);
        }
        catch
        {
            return await FailedAsync<MasterDataConversionRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                "persistence_unavailable",
                cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<decimal>> ConvertQuantityAsync(
        MasterDataRequestContext context,
        Guid conversionId,
        decimal quantity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var resource = Resource(MasterDataResourceKind.UnitOfMeasure, context, conversionId, "conversion");
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.View);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<decimal>(
                context,
                resource,
                MasterDataOperation.View,
                authorized.Decision,
                authorized.Code,
                cancellationToken);
        }

        try
        {
            var result = await persistence.ConvertQuantityAsync(
                context.TenantContext,
                conversionId,
                quantity,
                cancellationToken);
            return result.Succeeded
                ? MasterDataOperationResult<decimal>.Success(result.Quantity!.Value)
                : await FailedAsync<decimal>(
                    context,
                    resource,
                    MasterDataOperation.View,
                    result.Code,
                    cancellationToken);
        }
        catch
        {
            return await FailedAsync<decimal>(
                context,
                resource,
                MasterDataOperation.View,
                "persistence_unavailable",
                cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<IReadOnlyList<MasterDataAuditRecord>>> ReadAuditHistoryAsync(
        MasterDataRequestContext context,
        MasterDataResourceKind resourceKind,
        Guid? resourceId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (resourceKind is not (
            MasterDataResourceKind.ProductCategory
            or MasterDataResourceKind.UnitOfMeasure))
        {
            throw new ArgumentOutOfRangeException(nameof(resourceKind));
        }

        var resource = Resource(
            resourceKind,
            context,
            resourceId,
            resourceId is null ? "audit-history" : null);
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
                resourceKind,
                resourceId,
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

    private async Task<MasterDataHierarchyValidationResult> ValidateCategoryWriteAsync(
        TenantContext tenantContext,
        Guid categoryId,
        string code,
        LocalizedName name,
        Guid? parentCategoryId,
        CancellationToken cancellationToken)
    {
        var categories = await persistence.ListCategoriesAsync(tenantContext, cancellationToken);
        if (categories.Any(category =>
            !Equals(category.Id, categoryId)
            && (string.Equals(category.Code, code, StringComparison.OrdinalIgnoreCase)
                || NamesOverlap(category.Name, name))))
        {
            return MasterDataHierarchyValidationResult.Denied("category_duplicate");
        }

        var parentById = categories.ToDictionary(category => category.Id, category => category.ParentCategoryId);
        return hierarchyPolicy.Validate(categoryId, parentCategoryId, parentById);
    }

    private async Task<MasterDataHierarchyValidationResult> ValidateUnitNameAsync(
        TenantContext tenantContext,
        string code,
        LocalizedName name,
        CancellationToken cancellationToken,
        Guid? excludingId = null)
    {
        var units = await persistence.ListUnitsOfMeasureAsync(tenantContext, cancellationToken);
        return units.Any(unit =>
            unit.Id != excludingId
            && (string.Equals(unit.Code, code, StringComparison.OrdinalIgnoreCase)
                || NamesOverlap(unit.Name, name)))
            ? MasterDataHierarchyValidationResult.Denied("uom_duplicate")
            : MasterDataHierarchyValidationResult.Success();
    }

    private static bool NamesOverlap(LocalizedName left, LocalizedName right) =>
        left.English is not null && right.English is not null
            && string.Equals(left.English, right.English, StringComparison.OrdinalIgnoreCase)
        || left.Arabic is not null && right.Arabic is not null
            && string.Equals(left.Arabic, right.Arabic, StringComparison.OrdinalIgnoreCase);

    private static MasterDataResourceReference Resource(
        MasterDataResourceKind resourceKind,
        MasterDataRequestContext context,
        Guid? stableId,
        string? businessCode) => new(
            resourceKind,
            new TenantOwnership(context.TenantId.Value),
            stableId,
            businessCode,
            CategoryUomScopePolicy.CreateScope(context.TenantId));

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

    private static string CategorySummary(
        string code,
        LocalizedName name,
        MasterDataLifecycleState state) =>
        $"code={code};en={name.English ?? string.Empty};ar={name.Arabic ?? string.Empty};state={state}";

    private static string UomSummary(
        string code,
        LocalizedName name,
        MasterDataLifecycleState state) =>
        $"code={code};en={name.English ?? string.Empty};ar={name.Arabic ?? string.Empty};state={state}";

    private static void ValidateVersion(byte[] version)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (version.Length == 0 || version.Length > 64)
        {
            throw new ArgumentException("An optimistic-concurrency version is required.", nameof(version));
        }
    }

    private static FoundationAuditReason ReasonFor(string code) => code switch
    {
        "cross_tenant_target_denied" => FoundationAuditReason.CrossTenantTargetDenied,
        "permission_denied" => FoundationAuditReason.PermissionDenied,
        "authorization_denied" or "resource_scope_denied" => FoundationAuditReason.AuthorizationDenied,
        "category_not_found" or "uom_not_found" or "conversion_not_found" => FoundationAuditReason.NotFound,
        "concurrency_conflict" => FoundationAuditReason.ConcurrencyConflict,
        "persistence_unavailable"
            or "audit_unavailable"
            or "audit_evidence_invalid"
            or "audit_evidence_unavailable"
            or "audit_context_mismatch" => FoundationAuditReason.InternalFailure,
        "validation_failed"
            or "category_duplicate"
            or "uom_duplicate"
            or "parent_category_not_found"
            or "conversion_duplicate"
            or "precision_invalid"
            or "uom_in_use" => FoundationAuditReason.ValidationFailed,
        _ when code.Contains("inactive", StringComparison.Ordinal)
            || code.Contains("reference", StringComparison.Ordinal)
            || code.Contains("depth", StringComparison.Ordinal)
            || code.Contains("cycle", StringComparison.Ordinal) => FoundationAuditReason.ValidationFailed,
        _ => FoundationAuditReason.AuthorizationDenied
    };
}

#pragma warning restore CS1591
