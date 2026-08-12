#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.MasterData;

/// <summary>
/// MESP-119 application behavior. The service owns reusable Tenant Tax
/// identity, effective-dated rate versions, lifecycle, historical reference
/// evidence, and a deterministic calculation contract over an explicit
/// taxable base. Finance owns accounting effects and posted-document policy.
/// </summary>
public sealed class MasterDataTaxService
{
    private readonly MasterDataResourceAuthorizationService authorization;
    private readonly IMasterDataTaxPersistence persistence;

    public MasterDataTaxService(
        MasterDataResourceAuthorizationService authorization,
        IMasterDataTaxPersistence persistence)
    {
        this.authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
    }

    public async Task<MasterDataOperationResult<IReadOnlyList<MasterDataTaxRecord>>> ListTaxesAsync(
        MasterDataRequestContext context,
        CancellationToken cancellationToken = default)
    {
        var resource = Resource(context, null, "tax-list");
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.View);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<IReadOnlyList<MasterDataTaxRecord>>(
                context, resource, MasterDataOperation.View, authorized.Decision, authorized.Code, cancellationToken);
        }

        try
        {
            return MasterDataOperationResult<IReadOnlyList<MasterDataTaxRecord>>.Success(
                await persistence.ListTaxesAsync(context.TenantContext, cancellationToken));
        }
        catch
        {
            return await FailedAsync<IReadOnlyList<MasterDataTaxRecord>>(
                context, resource, MasterDataOperation.View, "persistence_unavailable", cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<MasterDataTaxRecord>> GetTaxAsync(
        MasterDataRequestContext context,
        Guid taxId,
        CancellationToken cancellationToken = default)
    {
        var resource = Resource(context, taxId, null);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.View);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataTaxRecord>(
                context, resource, MasterDataOperation.View, authorized.Decision, authorized.Code, cancellationToken);
        }

        try
        {
            var record = await persistence.FindTaxAsync(context.TenantContext, taxId, cancellationToken);
            return record is null
                ? await FailedAsync<MasterDataTaxRecord>(context, resource, MasterDataOperation.View, "tax_not_found", cancellationToken)
                : MasterDataOperationResult<MasterDataTaxRecord>.Success(record);
        }
        catch
        {
            return await FailedAsync<MasterDataTaxRecord>(
                context, resource, MasterDataOperation.View, "persistence_unavailable", cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<MasterDataTaxRecord>> GetTaxHistoryAsync(
        MasterDataRequestContext context,
        Guid taxId,
        CancellationToken cancellationToken = default)
    {
        // The aggregate response includes every immutable rate-version window;
        // returning the same tenant-filtered record keeps history and current
        // identity views consistent without inventing a second history model.
        return await GetTaxForOperationAsync(context, taxId, MasterDataOperation.View, cancellationToken);
    }

    public async Task<MasterDataOperationResult<MasterDataTaxReferenceRecord>> GetTaxReferenceAsync(
        MasterDataRequestContext context,
        Guid taxId,
        DateOnly effectiveOn,
        CancellationToken cancellationToken = default)
    {
        var resource = Resource(context, taxId, null);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.View);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataTaxReferenceRecord>(
                context, resource, MasterDataOperation.View, authorized.Decision, authorized.Code, cancellationToken);
        }

        try
        {
            if (effectiveOn == default)
            {
                return await FailedAsync<MasterDataTaxReferenceRecord>(
                    context, resource, MasterDataOperation.View, "validation_failed", cancellationToken);
            }

            var record = await persistence.FindTaxAsync(context.TenantContext, taxId, cancellationToken);
            if (record is null)
            {
                return await FailedAsync<MasterDataTaxReferenceRecord>(
                    context, resource, MasterDataOperation.View, "tax_not_found", cancellationToken);
            }

            if (record.LifecycleState != MasterDataLifecycleState.Active)
            {
                return await FailedAsync<MasterDataTaxReferenceRecord>(
                    context, resource, MasterDataOperation.View, "tax_inactive", cancellationToken);
            }

            var version = SelectVersion(record, effectiveOn);
            if (version is null)
            {
                return await FailedAsync<MasterDataTaxReferenceRecord>(
                    context, resource, MasterDataOperation.View, "tax_version_not_found", cancellationToken);
            }

            var snapshot = new ReferenceSnapshot(
                MasterDataResourceKind.Tax,
                record.Id,
                new TenantOwnership(record.TenantId.Value),
                version.VersionNumber,
                $"{record.Code};v{version.VersionNumber}",
                effectiveOn);
            return MasterDataOperationResult<MasterDataTaxReferenceRecord>.Success(
                new MasterDataTaxReferenceRecord(
                    record.Id,
                    record.TenantId,
                    record.Code,
                    record.CategoryCode,
                    record.CategoryName,
                    record.Name,
                    record.Applicability,
                    record.LifecycleState,
                    version.VersionNumber,
                    version.Id,
                    effectiveOn,
                    version,
                    snapshot,
                    record.Version));
        }
        catch
        {
            return await FailedAsync<MasterDataTaxReferenceRecord>(
                context, resource, MasterDataOperation.View, "persistence_unavailable", cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<MasterDataTaxCalculation>> CalculateTaxAsync(
        MasterDataRequestContext context,
        Guid taxId,
        TaxCalculationRequest request,
        CancellationToken cancellationToken = default)
    {
        var resource = Resource(context, taxId, null);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.View);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataTaxCalculation>(
                context, resource, MasterDataOperation.View, authorized.Decision, authorized.Code, cancellationToken);
        }

        try
        {
            MasterDataTaxValuePolicy.ValidateCalculation(request);
            var record = await persistence.FindTaxAsync(context.TenantContext, taxId, cancellationToken);
            if (record is null)
            {
                return await FailedAsync<MasterDataTaxCalculation>(
                    context, resource, MasterDataOperation.View, "tax_not_found", cancellationToken);
            }

            if (record.LifecycleState != MasterDataLifecycleState.Active)
            {
                return await FailedAsync<MasterDataTaxCalculation>(
                    context, resource, MasterDataOperation.View, "tax_inactive", cancellationToken);
            }

            if (record.Applicability != TaxDirection.Both
                && record.Applicability != request.TransactionDirection)
            {
                return await FailedAsync<MasterDataTaxCalculation>(
                    context, resource, MasterDataOperation.View, "tax_direction_not_applicable", cancellationToken);
            }

            var version = SelectVersion(record, request.EffectiveOn);
            if (version is null)
            {
                return await FailedAsync<MasterDataTaxCalculation>(
                    context, resource, MasterDataOperation.View, "tax_version_not_found", cancellationToken);
            }

            var currencyCode = MasterDataTaxValuePolicy.NormalizeCurrencyCode(request.CurrencyCode);
            var sourceLineage = MasterDataTaxValuePolicy.NormalizeLineage(request.SourceLineage);
            var taxAmount = decimal.Round(
                request.TaxableBase * version.RatePercentage / 100m,
                request.RoundingScale,
                request.RoundingMode == TaxRoundingMode.ToEven
                    ? MidpointRounding.ToEven
                    : MidpointRounding.AwayFromZero);
            var snapshot = new ReferenceSnapshot(
                MasterDataResourceKind.Tax,
                record.Id,
                new TenantOwnership(record.TenantId.Value),
                version.VersionNumber,
                $"{record.Code};v{version.VersionNumber}",
                request.EffectiveOn);
            return MasterDataOperationResult<MasterDataTaxCalculation>.Success(
                new MasterDataTaxCalculation(
                    record.Id,
                    record.TenantId,
                    record.Code,
                    record.CategoryCode,
                    record.Applicability,
                    request.TransactionDirection,
                    version.Id,
                    version.VersionNumber,
                    request.EffectiveOn,
                    version.EffectiveFrom,
                    version.EffectiveTo,
                    version.RatePercentage,
                    request.TaxableBase,
                    taxAmount,
                    currencyCode,
                    request.RoundingScale,
                    request.RoundingMode,
                    sourceLineage,
                    snapshot));
        }
        catch (ArgumentException)
        {
            return await FailedAsync<MasterDataTaxCalculation>(
                context, resource, MasterDataOperation.View, "validation_failed", cancellationToken);
        }
        catch
        {
            return await FailedAsync<MasterDataTaxCalculation>(
                context, resource, MasterDataOperation.View, "persistence_unavailable", cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<MasterDataTaxRecord>> CreateTaxAsync(
        MasterDataRequestContext context,
        CreateMasterDataTaxCommand command,
        CancellationToken cancellationToken = default)
    {
        var taxId = Guid.NewGuid();
        try
        {
            ArgumentNullException.ThrowIfNull(command);
            var code = MasterDataTaxValuePolicy.NormalizeCode(command.Code);
            var categoryCode = MasterDataTaxValuePolicy.NormalizeCategoryCode(command.CategoryCode);
            ArgumentNullException.ThrowIfNull(command.CategoryName);
            ArgumentNullException.ThrowIfNull(command.Name);
            MasterDataTaxValuePolicy.ValidateRateVersion(command.RateVersion);
            if (!Enum.IsDefined(command.Applicability))
            {
                throw new ArgumentException("The Tax applicability is invalid.");
            }

            command = command with { Code = code, CategoryCode = categoryCode };
        }
        catch (ArgumentException)
        {
            var invalidResource = Resource(context, taxId, "tax-create");
            return await FailedAsync<MasterDataTaxRecord>(
                context, invalidResource, MasterDataOperation.Create, "validation_failed", cancellationToken);
        }

        var resource = Resource(context, taxId, command.Code);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.Create);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataTaxRecord>(
                context, resource, MasterDataOperation.Create, authorized.Decision, authorized.Code, cancellationToken);
        }

        var evidence = CreateEvidence(
            context,
            resource,
            MasterDataOperation.Create,
            authorized.Decision,
            FoundationAuditReason.Allowed,
            afterSummary: TaxSummary(command.Code, command.CategoryCode, command.Applicability, MasterDataLifecycleState.Active, 1, command.RateVersion));
        try
        {
            return await CompletePersistenceAsync(
                context,
                resource,
                MasterDataOperation.Create,
                await persistence.CreateTaxAsync(context.TenantContext, taxId, command, evidence, cancellationToken),
                evidence,
                cancellationToken);
        }
        catch
        {
            return await FailedAsync<MasterDataTaxRecord>(
                context, resource, MasterDataOperation.Create, "persistence_unavailable", cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<MasterDataTaxRecord>> EditTaxAsync(
        MasterDataRequestContext context,
        EditMasterDataTaxCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(command);
            var code = MasterDataTaxValuePolicy.NormalizeCode(command.Code);
            var categoryCode = MasterDataTaxValuePolicy.NormalizeCategoryCode(command.CategoryCode);
            ArgumentNullException.ThrowIfNull(command.CategoryName);
            ArgumentNullException.ThrowIfNull(command.Name);
            MasterDataTaxValuePolicy.ValidateRateVersion(command.RateVersion);
            ValidateVersion(command.ExpectedVersion);
            if (!Enum.IsDefined(command.Applicability))
            {
                throw new ArgumentException("The Tax applicability is invalid.");
            }

            command = command with { Code = code, CategoryCode = categoryCode };
        }
        catch (ArgumentException)
        {
            var invalidResource = Resource(context, command?.TaxId ?? Guid.NewGuid(), "tax-edit");
            return await FailedAsync<MasterDataTaxRecord>(
                context, invalidResource, MasterDataOperation.Edit, "validation_failed", cancellationToken);
        }

        var resource = Resource(context, command.TaxId, command.Code);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.Edit);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataTaxRecord>(
                context, resource, MasterDataOperation.Edit, authorized.Decision, authorized.Code, cancellationToken);
        }

        MasterDataTaxRecord? current;
        try
        {
            current = await persistence.FindTaxAsync(context.TenantContext, command.TaxId, cancellationToken);
        }
        catch
        {
            return await FailedAsync<MasterDataTaxRecord>(
                context, resource, MasterDataOperation.Edit, "persistence_unavailable", cancellationToken);
        }

        if (current is null)
        {
            return await FailedAsync<MasterDataTaxRecord>(
                context, resource, MasterDataOperation.Edit, "tax_not_found", cancellationToken);
        }

        var evidence = CreateEvidence(
            context,
            resource,
            MasterDataOperation.Edit,
            authorized.Decision,
            FoundationAuditReason.Allowed,
            TaxSummary(current),
            TaxSummary(command.Code, command.CategoryCode, command.Applicability, current.LifecycleState, current.CurrentVersionNumber + 1, command.RateVersion));
        try
        {
            return await CompletePersistenceAsync(
                context,
                resource,
                MasterDataOperation.Edit,
                await persistence.EditTaxAsync(context.TenantContext, command, evidence, cancellationToken),
                evidence,
                cancellationToken);
        }
        catch
        {
            return await FailedAsync<MasterDataTaxRecord>(
                context, resource, MasterDataOperation.Edit, "persistence_unavailable", cancellationToken);
        }
    }

    public Task<MasterDataOperationResult<MasterDataTaxRecord>> DeactivateTaxAsync(
        MasterDataRequestContext context,
        Guid taxId,
        byte[] expectedVersion,
        CancellationToken cancellationToken = default) =>
        SetTaxLifecycleAsync(context, taxId, MasterDataLifecycleState.Inactive, MasterDataOperation.Deactivate, expectedVersion, cancellationToken);

    public Task<MasterDataOperationResult<MasterDataTaxRecord>> ReactivateTaxAsync(
        MasterDataRequestContext context,
        Guid taxId,
        byte[] expectedVersion,
        CancellationToken cancellationToken = default) =>
        SetTaxLifecycleAsync(context, taxId, MasterDataLifecycleState.Active, MasterDataOperation.Reactivate, expectedVersion, cancellationToken);

    public async Task<MasterDataOperationResult<IReadOnlyList<MasterDataAuditRecord>>> ReadAuditHistoryAsync(
        MasterDataRequestContext context,
        Guid? taxId = null,
        CancellationToken cancellationToken = default)
    {
        var resource = Resource(context, taxId, taxId is null ? "audit-history" : null);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.ViewAuditHistory);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<IReadOnlyList<MasterDataAuditRecord>>(
                context, resource, MasterDataOperation.ViewAuditHistory, authorized.Decision, authorized.Code, cancellationToken);
        }

        try
        {
            return MasterDataOperationResult<IReadOnlyList<MasterDataAuditRecord>>.Success(
                await persistence.ReadAuditHistoryAsync(context.TenantContext, taxId, cancellationToken));
        }
        catch
        {
            return await FailedAsync<IReadOnlyList<MasterDataAuditRecord>>(
                context, resource, MasterDataOperation.ViewAuditHistory, "persistence_unavailable", cancellationToken);
        }
    }

    private async Task<MasterDataOperationResult<MasterDataTaxRecord>> GetTaxForOperationAsync(
        MasterDataRequestContext context,
        Guid taxId,
        MasterDataOperation operation,
        CancellationToken cancellationToken)
    {
        var resource = Resource(context, taxId, null);
        var authorized = authorization.Authorize(context, resource, operation);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataTaxRecord>(
                context, resource, operation, authorized.Decision, authorized.Code, cancellationToken);
        }

        try
        {
            var record = await persistence.FindTaxAsync(context.TenantContext, taxId, cancellationToken);
            return record is null
                ? await FailedAsync<MasterDataTaxRecord>(context, resource, operation, "tax_not_found", cancellationToken)
                : MasterDataOperationResult<MasterDataTaxRecord>.Success(record);
        }
        catch
        {
            return await FailedAsync<MasterDataTaxRecord>(
                context, resource, operation, "persistence_unavailable", cancellationToken);
        }
    }

    private async Task<MasterDataOperationResult<MasterDataTaxRecord>> SetTaxLifecycleAsync(
        MasterDataRequestContext context,
        Guid taxId,
        MasterDataLifecycleState lifecycleState,
        MasterDataOperation operation,
        byte[] expectedVersion,
        CancellationToken cancellationToken)
    {
        var resource = Resource(context, taxId, null);
        var authorized = authorization.Authorize(context, resource, operation);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataTaxRecord>(
                context, resource, operation, authorized.Decision, authorized.Code, cancellationToken);
        }

        try
        {
            ValidateVersion(expectedVersion);
            var current = await persistence.FindTaxAsync(context.TenantContext, taxId, cancellationToken);
            if (current is null)
            {
                return await FailedAsync<MasterDataTaxRecord>(
                    context, resource, operation, "tax_not_found", cancellationToken);
            }

            var evidence = CreateEvidence(
                context,
                resource,
                operation,
                authorized.Decision,
                FoundationAuditReason.Allowed,
                TaxSummary(current),
                TaxSummary(current.Code, current.CategoryCode, current.Applicability, lifecycleState, current.CurrentVersionNumber, current.RateVersions.LastOrDefault()));
            return await CompletePersistenceAsync(
                context,
                resource,
                operation,
                await persistence.SetTaxLifecycleAsync(context.TenantContext, taxId, lifecycleState, expectedVersion, evidence, cancellationToken),
                evidence,
                cancellationToken);
        }
        catch (ArgumentException)
        {
            return await FailedAsync<MasterDataTaxRecord>(
                context, resource, operation, "validation_failed", cancellationToken);
        }
        catch
        {
            return await FailedAsync<MasterDataTaxRecord>(
                context, resource, operation, "persistence_unavailable", cancellationToken);
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

    private async Task<MasterDataOperationResult<T>> DeniedAsync<T>(
        MasterDataRequestContext context,
        MasterDataResourceReference resource,
        MasterDataOperation operation,
        MasterDataPolicyDecision decision,
        string code,
        CancellationToken cancellationToken)
    {
        var evidence = CreateEvidence(context, resource, operation, decision, ReasonFor(code));
        return await AppendDeniedEvidenceAsync<T>(context, evidence, code, cancellationToken);
    }

    private async Task<MasterDataOperationResult<T>> FailedAsync<T>(
        MasterDataRequestContext context,
        MasterDataResourceReference resource,
        MasterDataOperation operation,
        string code,
        CancellationToken cancellationToken)
    {
        var evidence = CreateEvidence(context, resource, operation, MasterDataPolicyDecision.Denied(code), ReasonFor(code));
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

    private static MasterDataResourceReference Resource(
        MasterDataRequestContext context,
        Guid? stableId,
        string? businessCode) => new(
        MasterDataResourceKind.Tax,
        new TenantOwnership(context.TenantId.Value),
        stableId,
        businessCode,
        TaxScopePolicy.CreateScope(new TenantId(context.TenantId.Value)));

    private static MasterDataAuditEvidence CreateEvidence(
        MasterDataRequestContext context,
        MasterDataResourceReference resource,
        MasterDataOperation operation,
        MasterDataPolicyDecision decision,
        FoundationAuditReason reason,
        string? beforeSummary = null,
        string? afterSummary = null) =>
        MasterDataAuditEvidenceFactory.Create(context, resource, operation, decision, reason, beforeSummary, afterSummary);

    private static MasterDataTaxRateVersionRecord? SelectVersion(
        MasterDataTaxRecord record,
        DateOnly effectiveOn) => record.RateVersions.SingleOrDefault(version =>
            version.EffectiveFrom <= effectiveOn
            && (version.EffectiveTo is null || effectiveOn <= version.EffectiveTo.Value));

    private static string TaxSummary(MasterDataTaxRecord record) =>
        TaxSummary(
            record.Code,
            record.CategoryCode,
            record.Applicability,
            record.LifecycleState,
            record.CurrentVersionNumber,
            record.RateVersions.LastOrDefault());

    private static string TaxSummary(
        string code,
        string categoryCode,
        TaxDirection applicability,
        MasterDataLifecycleState lifecycleState,
        int currentVersion,
        MasterDataTaxRateVersionRecord? version) =>
        $"code={code};category={categoryCode};applicability={applicability};state={lifecycleState};version={currentVersion};effective-from={version?.EffectiveFrom:yyyy-MM-dd};rate={version?.RatePercentage.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty}";

    private static string TaxSummary(
        string code,
        string categoryCode,
        TaxDirection applicability,
        MasterDataLifecycleState lifecycleState,
        int currentVersion,
        MasterDataTaxRateVersion version) =>
        $"code={code};category={categoryCode};applicability={applicability};state={lifecycleState};version={currentVersion};effective-from={version.EffectiveFrom:yyyy-MM-dd};rate={version.RatePercentage.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

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
        "tax_not_found" or "tax_version_not_found" => FoundationAuditReason.NotFound,
        "concurrency_conflict" or "tax_effective_overlap" => FoundationAuditReason.ConcurrencyConflict,
        "persistence_unavailable" or "audit_unavailable" or "audit_context_mismatch" => FoundationAuditReason.InternalFailure,
        "validation_failed" or "tax_duplicate" or "tax_lifecycle_no_change" or "tax_direction_not_applicable" or "tax_inactive" => FoundationAuditReason.ValidationFailed,
        _ => FoundationAuditReason.AuthorizationDenied
    };
}

#pragma warning restore CS1591
