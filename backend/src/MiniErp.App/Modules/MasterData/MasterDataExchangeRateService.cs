#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.MasterData;

/// <summary>
/// MESP-120 application behavior. This service owns Tenant-wide directional
/// Currency-pair identity, effective-dated manually/configured rate evidence,
/// lifecycle, and deterministic reference selection. Finance owns conversion,
/// rounding, realized/unrealized FX, revaluation, and accounting effects.
/// </summary>
public sealed class MasterDataExchangeRateService
{
    private readonly MasterDataResourceAuthorizationService authorization;
    private readonly IMasterDataExchangeRatePersistence persistence;

    public MasterDataExchangeRateService(
        MasterDataResourceAuthorizationService authorization,
        IMasterDataExchangeRatePersistence persistence)
    {
        this.authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
    }

    public async Task<MasterDataOperationResult<IReadOnlyList<MasterDataExchangeRateRecord>>> ListExchangeRatesAsync(
        MasterDataRequestContext context,
        CancellationToken cancellationToken = default)
    {
        var resource = Resource(context, null, "exchange-rate-list");
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.View);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<IReadOnlyList<MasterDataExchangeRateRecord>>(context, resource, MasterDataOperation.View, authorized.Decision, authorized.Code, cancellationToken);
        }

        try
        {
            return MasterDataOperationResult<IReadOnlyList<MasterDataExchangeRateRecord>>.Success(
                await persistence.ListExchangeRatesAsync(context.TenantContext, cancellationToken));
        }
        catch
        {
            return await FailedAsync<IReadOnlyList<MasterDataExchangeRateRecord>>(context, resource, MasterDataOperation.View, "persistence_unavailable", cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<MasterDataExchangeRateRecord>> GetExchangeRateAsync(
        MasterDataRequestContext context,
        Guid exchangeRateId,
        CancellationToken cancellationToken = default) =>
        await GetForOperationAsync(context, exchangeRateId, MasterDataOperation.View, cancellationToken);

    public async Task<MasterDataOperationResult<MasterDataExchangeRateRecord>> GetExchangeRateHistoryAsync(
        MasterDataRequestContext context,
        Guid exchangeRateId,
        CancellationToken cancellationToken = default) =>
        await GetForOperationAsync(context, exchangeRateId, MasterDataOperation.View, cancellationToken);

    public async Task<MasterDataOperationResult<MasterDataExchangeRateReferenceRecord>> GetExchangeRateReferenceAsync(
        MasterDataRequestContext context,
        Guid exchangeRateId,
        DateOnly effectiveOn,
        CancellationToken cancellationToken = default)
    {
        var resource = Resource(context, exchangeRateId, null);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.View);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataExchangeRateReferenceRecord>(context, resource, MasterDataOperation.View, authorized.Decision, authorized.Code, cancellationToken);
        }

        try
        {
            if (effectiveOn == default)
            {
                return await FailedAsync<MasterDataExchangeRateReferenceRecord>(context, resource, MasterDataOperation.View, "validation_failed", cancellationToken);
            }

            var record = await persistence.FindExchangeRateAsync(context.TenantContext, exchangeRateId, cancellationToken);
            if (record is null)
            {
                return await FailedAsync<MasterDataExchangeRateReferenceRecord>(context, resource, MasterDataOperation.View, "exchange_rate_not_found", cancellationToken);
            }

            if (record.LifecycleState != MasterDataLifecycleState.Active)
            {
                return await FailedAsync<MasterDataExchangeRateReferenceRecord>(context, resource, MasterDataOperation.View, "exchange_rate_inactive", cancellationToken);
            }

            var version = SelectVersion(record, effectiveOn);
            if (version is null)
            {
                return await FailedAsync<MasterDataExchangeRateReferenceRecord>(context, resource, MasterDataOperation.View, "exchange_rate_version_not_found", cancellationToken);
            }

            var snapshot = new ReferenceSnapshot(
                MasterDataResourceKind.ExchangeRate,
                record.Id,
                new TenantOwnership(record.TenantId.Value),
                version.VersionNumber,
                $"{version.SourceCurrencyCode}->{version.TargetCurrencyCode};v{version.VersionNumber}",
                effectiveOn);
            return MasterDataOperationResult<MasterDataExchangeRateReferenceRecord>.Success(
                new MasterDataExchangeRateReferenceRecord(
                    record.Id,
                    record.TenantId,
                    record.SourceCurrencyId,
                    record.TargetCurrencyId,
                    version.SourceCurrencyCode,
                    version.TargetCurrencyCode,
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
            return await FailedAsync<MasterDataExchangeRateReferenceRecord>(context, resource, MasterDataOperation.View, "persistence_unavailable", cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<MasterDataExchangeRateRecord>> CreateExchangeRateAsync(
        MasterDataRequestContext context,
        CreateMasterDataExchangeRateCommand command,
        CancellationToken cancellationToken = default)
    {
        var exchangeRateId = Guid.NewGuid();
        try
        {
            ArgumentNullException.ThrowIfNull(command);
            MasterDataExchangeRateValuePolicy.Validate(
                command.SourceCurrencyId,
                command.TargetCurrencyId,
                command.EffectiveFrom,
                command.EffectiveTo,
                command.Rate,
                command.RateScale,
                command.Provenance,
                command.SourceNotes);
            command = command with { SourceNotes = MasterDataExchangeRateValuePolicy.NormalizeSourceNotes(command.SourceNotes) };
        }
        catch (ArgumentException)
        {
            return await FailedAsync<MasterDataExchangeRateRecord>(context, Resource(context, exchangeRateId, "exchange-rate-create"), MasterDataOperation.Create, "validation_failed", cancellationToken);
        }

        var resource = Resource(context, exchangeRateId, PairCode(command.SourceCurrencyId, command.TargetCurrencyId));
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.Create);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataExchangeRateRecord>(context, resource, MasterDataOperation.Create, authorized.Decision, authorized.Code, cancellationToken);
        }

        var evidence = CreateEvidence(
            context,
            resource,
            MasterDataOperation.Create,
            authorized.Decision,
            FoundationAuditReason.Allowed,
            afterSummary: ExchangeRateSummary(command.SourceCurrencyId, command.TargetCurrencyId, MasterDataLifecycleState.Active, 1, command));
        try
        {
            return await CompletePersistenceAsync(
                context,
                resource,
                MasterDataOperation.Create,
                await persistence.CreateExchangeRateAsync(context.TenantContext, exchangeRateId, command, evidence, cancellationToken),
                evidence,
                cancellationToken);
        }
        catch
        {
            return await FailedAsync<MasterDataExchangeRateRecord>(context, resource, MasterDataOperation.Create, "persistence_unavailable", cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<MasterDataExchangeRateRecord>> EditExchangeRateAsync(
        MasterDataRequestContext context,
        EditMasterDataExchangeRateCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(command);
            MasterDataExchangeRateValuePolicy.Validate(
                command.SourceCurrencyId,
                command.TargetCurrencyId,
                command.EffectiveFrom,
                command.EffectiveTo,
                command.Rate,
                command.RateScale,
                command.Provenance,
                command.SourceNotes);
            MasterDataExchangeRateValuePolicy.ValidateVersion(command.ExpectedVersion);
            command = command with { SourceNotes = MasterDataExchangeRateValuePolicy.NormalizeSourceNotes(command.SourceNotes) };
        }
        catch (ArgumentException)
        {
            return await FailedAsync<MasterDataExchangeRateRecord>(context, Resource(context, command?.ExchangeRateId ?? Guid.NewGuid(), "exchange-rate-edit"), MasterDataOperation.Edit, "validation_failed", cancellationToken);
        }

        var resource = Resource(context, command.ExchangeRateId, PairCode(command.SourceCurrencyId, command.TargetCurrencyId));
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.Edit);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataExchangeRateRecord>(context, resource, MasterDataOperation.Edit, authorized.Decision, authorized.Code, cancellationToken);
        }

        MasterDataExchangeRateRecord? current;
        try
        {
            current = await persistence.FindExchangeRateAsync(context.TenantContext, command.ExchangeRateId, cancellationToken);
        }
        catch
        {
            return await FailedAsync<MasterDataExchangeRateRecord>(context, resource, MasterDataOperation.Edit, "persistence_unavailable", cancellationToken);
        }

        if (current is null)
        {
            return await FailedAsync<MasterDataExchangeRateRecord>(context, resource, MasterDataOperation.Edit, "exchange_rate_not_found", cancellationToken);
        }

        var evidence = CreateEvidence(
            context,
            resource,
            MasterDataOperation.Edit,
            authorized.Decision,
            FoundationAuditReason.Allowed,
            ExchangeRateSummary(current),
            ExchangeRateSummary(command.SourceCurrencyId, command.TargetCurrencyId, current.LifecycleState, current.CurrentVersionNumber + 1, command));
        try
        {
            return await CompletePersistenceAsync(
                context,
                resource,
                MasterDataOperation.Edit,
                await persistence.EditExchangeRateAsync(context.TenantContext, command, evidence, cancellationToken),
                evidence,
                cancellationToken);
        }
        catch
        {
            return await FailedAsync<MasterDataExchangeRateRecord>(context, resource, MasterDataOperation.Edit, "persistence_unavailable", cancellationToken);
        }
    }

    public Task<MasterDataOperationResult<MasterDataExchangeRateRecord>> DeactivateExchangeRateAsync(
        MasterDataRequestContext context,
        Guid exchangeRateId,
        byte[] expectedVersion,
        CancellationToken cancellationToken = default) =>
        SetLifecycleAsync(context, exchangeRateId, MasterDataLifecycleState.Inactive, MasterDataOperation.Deactivate, expectedVersion, cancellationToken);

    public Task<MasterDataOperationResult<MasterDataExchangeRateRecord>> ReactivateExchangeRateAsync(
        MasterDataRequestContext context,
        Guid exchangeRateId,
        byte[] expectedVersion,
        CancellationToken cancellationToken = default) =>
        SetLifecycleAsync(context, exchangeRateId, MasterDataLifecycleState.Active, MasterDataOperation.Reactivate, expectedVersion, cancellationToken);

    public async Task<MasterDataOperationResult<IReadOnlyList<MasterDataAuditRecord>>> ReadAuditHistoryAsync(
        MasterDataRequestContext context,
        Guid? exchangeRateId = null,
        CancellationToken cancellationToken = default)
    {
        var resource = Resource(context, exchangeRateId, exchangeRateId is null ? "audit-history" : null);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.ViewAuditHistory);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<IReadOnlyList<MasterDataAuditRecord>>(context, resource, MasterDataOperation.ViewAuditHistory, authorized.Decision, authorized.Code, cancellationToken);
        }

        try
        {
            return MasterDataOperationResult<IReadOnlyList<MasterDataAuditRecord>>.Success(
                await persistence.ReadAuditHistoryAsync(context.TenantContext, exchangeRateId, cancellationToken));
        }
        catch
        {
            return await FailedAsync<IReadOnlyList<MasterDataAuditRecord>>(context, resource, MasterDataOperation.ViewAuditHistory, "persistence_unavailable", cancellationToken);
        }
    }

    private async Task<MasterDataOperationResult<MasterDataExchangeRateRecord>> GetForOperationAsync(
        MasterDataRequestContext context,
        Guid exchangeRateId,
        MasterDataOperation operation,
        CancellationToken cancellationToken)
    {
        var resource = Resource(context, exchangeRateId, null);
        var authorized = authorization.Authorize(context, resource, operation);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataExchangeRateRecord>(context, resource, operation, authorized.Decision, authorized.Code, cancellationToken);
        }

        try
        {
            var record = await persistence.FindExchangeRateAsync(context.TenantContext, exchangeRateId, cancellationToken);
            return record is null
                ? await FailedAsync<MasterDataExchangeRateRecord>(context, resource, operation, "exchange_rate_not_found", cancellationToken)
                : MasterDataOperationResult<MasterDataExchangeRateRecord>.Success(record);
        }
        catch
        {
            return await FailedAsync<MasterDataExchangeRateRecord>(context, resource, operation, "persistence_unavailable", cancellationToken);
        }
    }

    private async Task<MasterDataOperationResult<MasterDataExchangeRateRecord>> SetLifecycleAsync(
        MasterDataRequestContext context,
        Guid exchangeRateId,
        MasterDataLifecycleState lifecycleState,
        MasterDataOperation operation,
        byte[] expectedVersion,
        CancellationToken cancellationToken)
    {
        var resource = Resource(context, exchangeRateId, null);
        var authorized = authorization.Authorize(context, resource, operation);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<MasterDataExchangeRateRecord>(context, resource, operation, authorized.Decision, authorized.Code, cancellationToken);
        }

        try
        {
            MasterDataExchangeRateValuePolicy.ValidateVersion(expectedVersion);
            var current = await persistence.FindExchangeRateAsync(context.TenantContext, exchangeRateId, cancellationToken);
            if (current is null)
            {
                return await FailedAsync<MasterDataExchangeRateRecord>(context, resource, operation, "exchange_rate_not_found", cancellationToken);
            }

            var evidence = CreateEvidence(
                context,
                resource,
                operation,
                authorized.Decision,
                FoundationAuditReason.Allowed,
                ExchangeRateSummary(current),
                ExchangeRateSummary(current with { LifecycleState = lifecycleState }));
            return await CompletePersistenceAsync(
                context,
                resource,
                operation,
                await persistence.SetExchangeRateLifecycleAsync(context.TenantContext, exchangeRateId, lifecycleState, expectedVersion, evidence, cancellationToken),
                evidence,
                cancellationToken);
        }
        catch (ArgumentException)
        {
            return await FailedAsync<MasterDataExchangeRateRecord>(context, resource, operation, "validation_failed", cancellationToken);
        }
        catch
        {
            return await FailedAsync<MasterDataExchangeRateRecord>(context, resource, operation, "persistence_unavailable", cancellationToken);
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
        MasterDataResourceKind.ExchangeRate,
        new TenantOwnership(context.TenantId.Value),
        stableId,
        businessCode,
        ExchangeRateScopePolicy.CreateScope(new TenantId(context.TenantId.Value)));

    private static MasterDataAuditEvidence CreateEvidence(
        MasterDataRequestContext context,
        MasterDataResourceReference resource,
        MasterDataOperation operation,
        MasterDataPolicyDecision decision,
        FoundationAuditReason reason,
        string? beforeSummary = null,
        string? afterSummary = null) =>
        MasterDataAuditEvidenceFactory.Create(context, resource, operation, decision, reason, beforeSummary, afterSummary);

    private static MasterDataExchangeRateVersionRecord? SelectVersion(
        MasterDataExchangeRateRecord record,
        DateOnly effectiveOn) => record.Versions.SingleOrDefault(version =>
        version.EffectiveFrom <= effectiveOn
        && (version.EffectiveTo is null || effectiveOn <= version.EffectiveTo.Value));

    private static string PairCode(Guid sourceCurrencyId, Guid targetCurrencyId) =>
        $"{sourceCurrencyId:N}->{targetCurrencyId:N}";

    private static string ExchangeRateSummary(MasterDataExchangeRateRecord record) =>
        $"pair={record.SourceCurrencyId:N}->{record.TargetCurrencyId:N};state={record.LifecycleState};version={record.CurrentVersionNumber};versions={record.Versions.Count}";

    private static string ExchangeRateSummary(
        Guid sourceCurrencyId,
        Guid targetCurrencyId,
        MasterDataLifecycleState lifecycleState,
        int currentVersion,
        CreateMasterDataExchangeRateCommand command) =>
        $"pair={sourceCurrencyId:N}->{targetCurrencyId:N};state={lifecycleState};version={currentVersion};effective-from={command.EffectiveFrom:yyyy-MM-dd};rate={command.Rate.ToString(System.Globalization.CultureInfo.InvariantCulture)};scale={command.RateScale};provenance={command.Provenance}";

    private static string ExchangeRateSummary(
        Guid sourceCurrencyId,
        Guid targetCurrencyId,
        MasterDataLifecycleState lifecycleState,
        int currentVersion,
        EditMasterDataExchangeRateCommand command) =>
        $"pair={sourceCurrencyId:N}->{targetCurrencyId:N};state={lifecycleState};version={currentVersion};effective-from={command.EffectiveFrom:yyyy-MM-dd};rate={command.Rate.ToString(System.Globalization.CultureInfo.InvariantCulture)};scale={command.RateScale};provenance={command.Provenance}";

    private static FoundationAuditReason ReasonFor(string code) => code switch
    {
        "cross_tenant_target_denied" => FoundationAuditReason.CrossTenantTargetDenied,
        "permission_denied" => FoundationAuditReason.PermissionDenied,
        "authorization_denied" or "resource_scope_denied" => FoundationAuditReason.AuthorizationDenied,
        "exchange_rate_not_found" or "exchange_rate_version_not_found" => FoundationAuditReason.NotFound,
        "concurrency_conflict" or "exchange_rate_effective_overlap" or "exchange_rate_pair_immutable" => FoundationAuditReason.ConcurrencyConflict,
        "persistence_unavailable" or "audit_unavailable" or "audit_context_mismatch" => FoundationAuditReason.InternalFailure,
        "validation_failed" or "exchange_rate_duplicate" or "exchange_rate_currency_not_found" or "exchange_rate_currency_inactive" or "exchange_rate_lifecycle_no_change" or "exchange_rate_inactive" => FoundationAuditReason.ValidationFailed,
        _ => FoundationAuditReason.AuthorizationDenied
    };
}

#pragma warning restore CS1591
