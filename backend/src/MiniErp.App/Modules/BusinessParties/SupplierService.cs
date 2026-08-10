#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.BusinessParties;

/// <summary>
/// Application behavior for M95-SL-04. Supplier is an external Business Party
/// role and never an authenticated User, credential, membership, or unified
/// Party entity.
/// </summary>
public sealed class SupplierService
{
    private const int MaximumAuditValueLength = 512;

    private readonly SupplierResourceAuthorizationService authorization;
    private readonly ISupplierPersistence persistence;
    private readonly ISupplierCrossRoleMatchPolicy crossRoleMatchPolicy;

    public SupplierService(
        SupplierResourceAuthorizationService authorization,
        ISupplierPersistence persistence,
        ISupplierCrossRoleMatchPolicy? crossRoleMatchPolicy = null)
    {
        this.authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        this.crossRoleMatchPolicy = crossRoleMatchPolicy ?? new SupplierCrossRoleMatchPolicyUnavailable();
    }

    public async Task<MasterDataOperationResult<IReadOnlyList<SupplierRecord>>> ListSuppliersAsync(
        MasterDataRequestContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var resource = Resource(context, null, "supplier-list");
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.View);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<IReadOnlyList<SupplierRecord>>(
                context,
                resource,
                MasterDataOperation.View,
                authorized.Decision,
                authorized.Code,
                cancellationToken);
        }

        try
        {
            var records = await persistence.ListSuppliersAsync(
                context.TenantContext,
                cancellationToken);
            return MasterDataOperationResult<IReadOnlyList<SupplierRecord>>.Success(records);
        }
        catch
        {
            return await FailedAsync<IReadOnlyList<SupplierRecord>>(
                context,
                resource,
                MasterDataOperation.View,
                "persistence_unavailable",
                cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<SupplierRecord>> GetSupplierAsync(
        MasterDataRequestContext context,
        Guid supplierId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var resource = Resource(
            context,
            supplierId == Guid.Empty ? Guid.NewGuid() : supplierId,
            "supplier-read");
        if (supplierId == Guid.Empty)
        {
            return await FailedAsync<SupplierRecord>(
                context,
                resource,
                MasterDataOperation.View,
                "validation_failed",
                cancellationToken);
        }

        var authorized = authorization.Authorize(context, resource, MasterDataOperation.View);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<SupplierRecord>(
                context,
                resource,
                MasterDataOperation.View,
                authorized.Decision,
                authorized.Code,
                cancellationToken);
        }

        try
        {
            var record = await persistence.FindSupplierAsync(
                context.TenantContext,
                supplierId,
                cancellationToken);
            if (record is null)
            {
                return await FailedAsync<SupplierRecord>(
                    context,
                    resource,
                    MasterDataOperation.View,
                    "supplier_not_found",
                    cancellationToken);
            }

            if (record.TenantId.Value != context.TenantId.Value)
            {
                return await FailedAsync<SupplierRecord>(
                    context,
                    resource,
                    MasterDataOperation.View,
                    "cross_tenant_target_denied",
                    cancellationToken);
            }

            return MasterDataOperationResult<SupplierRecord>.Success(record);
        }
        catch
        {
            return await FailedAsync<SupplierRecord>(
                context,
                resource,
                MasterDataOperation.View,
                "persistence_unavailable",
                cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<SupplierRecord>> CreateSupplierAsync(
        MasterDataRequestContext context,
        CreateSupplierCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);

        var supplierId = Guid.NewGuid();
        var resource = Resource(context, supplierId, "supplier-create");
        string code;
        LocalizedName? tradingName;
        string? registrationReference;
        IReadOnlyList<SupplierContactCommand> contacts;
        try
        {
            code = SupplierValuePolicy.NormalizeCode(command.Code);
            SupplierValuePolicy.ValidateName(command.LegalName, nameof(command.LegalName));
            tradingName = SupplierValuePolicy.NormalizeTradingName(command.TradingName);
            registrationReference = SupplierValuePolicy.NormalizeRegistrationReference(command.RegistrationReference);
            contacts = SupplierValuePolicy.NormalizeContacts(command.Contacts);
        }
        catch (ArgumentException)
        {
            return await FailedAsync<SupplierRecord>(
                context,
                resource,
                MasterDataOperation.Create,
                "validation_failed",
                cancellationToken);
        }

        resource = Resource(context, supplierId, code);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.Create);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<SupplierRecord>(
                context,
                resource,
                MasterDataOperation.Create,
                authorized.Decision,
                authorized.Code,
                cancellationToken);
        }

        var normalized = command with
        {
            Code = code,
            TradingName = tradingName,
            RegistrationReference = registrationReference,
            Contacts = contacts
        };
        var crossRoleReview = EvaluateCrossRoleReview(context, normalized);
        var evidence = CreateEvidence(
            context,
            resource,
            MasterDataOperation.Create,
            authorized.Decision,
            FoundationAuditReason.Allowed,
            afterSummary: SupplierSummary(normalized, MasterDataLifecycleState.Active, crossRoleReview));

        try
        {
            var result = await persistence.CreateSupplierAsync(
                context.TenantContext,
                supplierId,
                normalized,
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
            return await FailedAsync<SupplierRecord>(
                context,
                resource,
                MasterDataOperation.Create,
                "persistence_unavailable",
                cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<SupplierRecord>> EditSupplierAsync(
        MasterDataRequestContext context,
        EditSupplierCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);

        var resource = Resource(
            context,
            command.SupplierId == Guid.Empty ? Guid.NewGuid() : command.SupplierId,
            "supplier-edit");
        string code;
        LocalizedName? tradingName;
        string? registrationReference;
        IReadOnlyList<SupplierContactCommand> contacts;
        try
        {
            if (command.SupplierId == Guid.Empty)
            {
                throw new ArgumentException("A Supplier identifier is required.", nameof(command));
            }

            ValidateVersion(command.ExpectedVersion);
            code = SupplierValuePolicy.NormalizeCode(command.Code);
            SupplierValuePolicy.ValidateName(command.LegalName, nameof(command.LegalName));
            tradingName = SupplierValuePolicy.NormalizeTradingName(command.TradingName);
            registrationReference = SupplierValuePolicy.NormalizeRegistrationReference(command.RegistrationReference);
            contacts = SupplierValuePolicy.NormalizeContacts(command.Contacts);
        }
        catch (ArgumentException)
        {
            return await FailedAsync<SupplierRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                "validation_failed",
                cancellationToken);
        }

        resource = Resource(context, command.SupplierId, code);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.Edit);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<SupplierRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                authorized.Decision,
                authorized.Code,
                cancellationToken);
        }

        SupplierRecord current;
        try
        {
            current = await persistence.FindSupplierAsync(
                context.TenantContext,
                command.SupplierId,
                cancellationToken) ?? throw new SupplierNotFoundException();
        }
        catch (SupplierNotFoundException)
        {
            return await FailedAsync<SupplierRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                "supplier_not_found",
                cancellationToken);
        }
        catch
        {
            return await FailedAsync<SupplierRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                "persistence_unavailable",
                cancellationToken);
        }

        if (current.TenantId.Value != context.TenantId.Value)
        {
            return await FailedAsync<SupplierRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                "cross_tenant_target_denied",
                cancellationToken);
        }

        var normalized = command with
        {
            Code = code,
            TradingName = tradingName,
            RegistrationReference = registrationReference,
            Contacts = contacts
        };
        var crossRoleReview = EvaluateCrossRoleReview(context, normalized);
        var evidence = CreateEvidence(
            context,
            resource,
            MasterDataOperation.Edit,
            authorized.Decision,
            FoundationAuditReason.Allowed,
            beforeSummary: SupplierSummary(current),
            afterSummary: SupplierSummary(normalized, current.LifecycleState, crossRoleReview));

        try
        {
            var result = await persistence.EditSupplierAsync(
                context.TenantContext,
                normalized,
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
            return await FailedAsync<SupplierRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                "persistence_unavailable",
                cancellationToken);
        }
    }

    public Task<MasterDataOperationResult<SupplierRecord>> ReactivateSupplierAsync(
        MasterDataRequestContext context,
        Guid supplierId,
        byte[] expectedVersion,
        CancellationToken cancellationToken = default) =>
        SetSupplierLifecycleAsync(
            context,
            supplierId,
            MasterDataLifecycleState.Active,
            expectedVersion,
            reason: null,
            MasterDataOperation.Reactivate,
            cancellationToken);

    public Task<MasterDataOperationResult<SupplierRecord>> DeactivateSupplierAsync(
        MasterDataRequestContext context,
        Guid supplierId,
        byte[] expectedVersion,
        string? reason,
        CancellationToken cancellationToken = default) =>
        SetSupplierLifecycleAsync(
            context,
            supplierId,
            MasterDataLifecycleState.Inactive,
            expectedVersion,
            reason,
            MasterDataOperation.Deactivate,
            cancellationToken);

    public async Task<MasterDataOperationResult<SupplierRecord>> SetSupplierLifecycleAsync(
        MasterDataRequestContext context,
        Guid supplierId,
        MasterDataLifecycleState lifecycleState,
        byte[] expectedVersion,
        string? reason,
        MasterDataOperation operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var resource = Resource(
            context,
            supplierId == Guid.Empty ? Guid.NewGuid() : supplierId,
            businessCode: null);
        if (supplierId == Guid.Empty
            || lifecycleState is not (MasterDataLifecycleState.Active or MasterDataLifecycleState.Inactive)
            || operation is not (MasterDataOperation.Deactivate or MasterDataOperation.Reactivate)
            || operation == MasterDataOperation.Deactivate && lifecycleState != MasterDataLifecycleState.Inactive
            || operation == MasterDataOperation.Reactivate && lifecycleState != MasterDataLifecycleState.Active)
        {
            return await FailedAsync<SupplierRecord>(
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
                return await FailedAsync<SupplierRecord>(
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
            return await FailedAsync<SupplierRecord>(
                context,
                resource,
                operation,
                "validation_failed",
                cancellationToken);
        }

        var authorized = authorization.Authorize(context, resource, operation);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<SupplierRecord>(
                context,
                resource,
                operation,
                authorized.Decision,
                authorized.Code,
                cancellationToken);
        }

        SupplierRecord current;
        try
        {
            current = await persistence.FindSupplierAsync(
                context.TenantContext,
                supplierId,
                cancellationToken) ?? throw new SupplierNotFoundException();
        }
        catch (SupplierNotFoundException)
        {
            return await FailedAsync<SupplierRecord>(
                context,
                resource,
                operation,
                "supplier_not_found",
                cancellationToken);
        }
        catch
        {
            return await FailedAsync<SupplierRecord>(
                context,
                resource,
                operation,
                "persistence_unavailable",
                cancellationToken);
        }

        if (current.TenantId.Value != context.TenantId.Value)
        {
            return await FailedAsync<SupplierRecord>(
                context,
                resource,
                operation,
                "cross_tenant_target_denied",
                cancellationToken);
        }

        if (current.LifecycleState == lifecycleState)
        {
            return await FailedAsync<SupplierRecord>(
                context,
                resource,
                operation,
                "supplier_lifecycle_no_change",
                cancellationToken);
        }

        var afterSummary = $"{SupplierSummary(current with { LifecycleState = lifecycleState })};reason={AuditValue(normalizedReason)}";
        var evidence = CreateEvidence(
            context,
            resource,
            operation,
            authorized.Decision,
            FoundationAuditReason.Allowed,
            beforeSummary: SupplierSummary(current),
            afterSummary: afterSummary);

        try
        {
            var result = await persistence.SetSupplierLifecycleAsync(
                context.TenantContext,
                supplierId,
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
            return await FailedAsync<SupplierRecord>(
                context,
                resource,
                operation,
                "persistence_unavailable",
                cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<IReadOnlyList<MasterDataAuditRecord>>> ReadAuditHistoryAsync(
        MasterDataRequestContext context,
        Guid supplierId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var resource = Resource(
            context,
            supplierId == Guid.Empty ? Guid.NewGuid() : supplierId,
            businessCode: null);
        if (supplierId == Guid.Empty)
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
                supplierId,
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
        return failed;
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

    private SupplierCrossRoleMatchReview EvaluateCrossRoleReview(
        MasterDataRequestContext context,
        CreateSupplierCommand command)
    {
        try
        {
            return crossRoleMatchPolicy.Evaluate(
                context,
                SupplierValuePolicy.NameKey(command.LegalName.English)!,
                SupplierValuePolicy.NameKey(command.RegistrationReference));
        }
        catch
        {
            return SupplierCrossRoleMatchReview.Unavailable();
        }
    }

    private SupplierCrossRoleMatchReview EvaluateCrossRoleReview(
        MasterDataRequestContext context,
        EditSupplierCommand command)
    {
        try
        {
            return crossRoleMatchPolicy.Evaluate(
                context,
                SupplierValuePolicy.NameKey(command.LegalName.English)!,
                SupplierValuePolicy.NameKey(command.RegistrationReference));
        }
        catch
        {
            return SupplierCrossRoleMatchReview.Unavailable();
        }
    }

    private static MasterDataResourceReference Resource(
        MasterDataRequestContext context,
        Guid? stableId,
        string? businessCode) => new(
        MasterDataResourceKind.Supplier,
        new TenantOwnership(context.TenantId.Value),
        stableId,
        businessCode,
        SupplierScopePolicy.CreateScope(context.TenantId));

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

    private static string SupplierSummary(SupplierRecord record) =>
        $"code={record.Code};legal-en={AuditValue(record.LegalName.English)};legal-ar={AuditValue(record.LegalName.Arabic)};"
        + $"trading-en={AuditValue(record.TradingName?.English)};trading-ar={AuditValue(record.TradingName?.Arabic)};"
        + $"registration={AuditValue(record.RegistrationReference)};contacts={record.Contacts.Count};state={record.LifecycleState}";

    private static string SupplierSummary(
        CreateSupplierCommand command,
        MasterDataLifecycleState lifecycleState,
        SupplierCrossRoleMatchReview review) =>
        $"code={command.Code};legal-en={AuditValue(command.LegalName.English)};legal-ar={AuditValue(command.LegalName.Arabic)};"
        + $"trading-en={AuditValue(command.TradingName?.English)};trading-ar={AuditValue(command.TradingName?.Arabic)};"
        + $"registration={AuditValue(command.RegistrationReference)};contacts={command.Contacts.Count};state={lifecycleState};"
        + $"cross-role-review={review.Code};review-count={review.EvidenceCount}";

    private static string SupplierSummary(
        EditSupplierCommand command,
        MasterDataLifecycleState lifecycleState,
        SupplierCrossRoleMatchReview review) =>
        SupplierSummary(
            new CreateSupplierCommand(
                command.Code,
                command.LegalName,
                command.TradingName,
                command.RegistrationReference,
                command.Contacts),
            lifecycleState,
            review);

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
        if (normalized.Length > MaximumAuditValueLength || normalized.Any(char.IsControl))
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
            or "resource_policy_not_configured"
            or "approval_required"
            or "approval_pending"
            or "approval_rejected"
            or "approval_policy_not_configured"
            or "approval_identity_missing"
            or "approval_policy_invalid"
            or "self_approval_denied" => FoundationAuditReason.AuthorizationDenied,
        "supplier_not_found" => FoundationAuditReason.NotFound,
        "concurrency_conflict" => FoundationAuditReason.ConcurrencyConflict,
        "supplier_lifecycle_no_change"
            or "deactivation_reason_required" => FoundationAuditReason.LifecycleDenied,
        "validation_failed" => FoundationAuditReason.ValidationFailed,
        "supplier_duplicate"
            or "supplier_code_duplicate"
            or "supplier_registration_duplicate" => FoundationAuditReason.ValidationFailed,
        "permission_unavailable"
            or "scope_policy_unavailable"
            or "approval_policy_unavailable"
            or "resource_policy_unavailable"
            or "authorization_operation_unmapped"
            or "persistence_unavailable"
            or "audit_unavailable"
            or "audit_evidence_invalid"
            or "audit_evidence_unavailable"
            or "cross_role_review_unavailable" => FoundationAuditReason.InternalFailure,
        _ => FoundationAuditReason.InternalFailure
    };

    private sealed class SupplierNotFoundException : Exception;
}

#pragma warning restore CS1591
