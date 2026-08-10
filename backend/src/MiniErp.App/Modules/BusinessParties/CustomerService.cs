#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.BusinessParties;

/// <summary>
/// Application behavior for M95-SL-05. Business Customer is an external B2B
/// role and never an authenticated User, credential, membership, consumer,
/// portal account or unified Party entity.
/// </summary>
public sealed class CustomerService
{
    private const int MaximumAuditValueLength = 512;

    private readonly CustomerResourceAuthorizationService authorization;
    private readonly ICustomerPersistence persistence;
    private readonly ISupplierCrossRoleMatchPolicy crossRoleMatchPolicy;

    public CustomerService(
        CustomerResourceAuthorizationService authorization,
        ICustomerPersistence persistence,
        ISupplierCrossRoleMatchPolicy? crossRoleMatchPolicy = null)
    {
        this.authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        this.crossRoleMatchPolicy = crossRoleMatchPolicy ?? new SupplierCrossRoleMatchPolicyUnavailable();
    }

    public async Task<MasterDataOperationResult<IReadOnlyList<CustomerRecord>>> ListCustomersAsync(
        MasterDataRequestContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var resource = Resource(context, null, "customer-list");
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.View);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<IReadOnlyList<CustomerRecord>>(
                context,
                resource,
                MasterDataOperation.View,
                authorized.Decision,
                authorized.Code,
                cancellationToken);
        }

        try
        {
            var records = await persistence.ListCustomersAsync(
                context.TenantContext,
                cancellationToken);
            return MasterDataOperationResult<IReadOnlyList<CustomerRecord>>.Success(records);
        }
        catch
        {
            return await FailedAsync<IReadOnlyList<CustomerRecord>>(
                context,
                resource,
                MasterDataOperation.View,
                "persistence_unavailable",
                cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<CustomerRecord>> GetCustomerAsync(
        MasterDataRequestContext context,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var resource = Resource(
            context,
            customerId == Guid.Empty ? Guid.NewGuid() : customerId,
            "customer-read");
        if (customerId == Guid.Empty)
        {
            return await FailedAsync<CustomerRecord>(
                context,
                resource,
                MasterDataOperation.View,
                "validation_failed",
                cancellationToken);
        }

        var authorized = authorization.Authorize(context, resource, MasterDataOperation.View);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<CustomerRecord>(
                context,
                resource,
                MasterDataOperation.View,
                authorized.Decision,
                authorized.Code,
                cancellationToken);
        }

        try
        {
            var record = await persistence.FindCustomerAsync(
                context.TenantContext,
                customerId,
                cancellationToken);
            if (record is null)
            {
                return await FailedAsync<CustomerRecord>(
                    context,
                    resource,
                    MasterDataOperation.View,
                    "customer_not_found",
                    cancellationToken);
            }

            if (record.TenantId.Value != context.TenantId.Value)
            {
                return await FailedAsync<CustomerRecord>(
                    context,
                    resource,
                    MasterDataOperation.View,
                    "cross_tenant_target_denied",
                    cancellationToken);
            }

            return MasterDataOperationResult<CustomerRecord>.Success(record);
        }
        catch
        {
            return await FailedAsync<CustomerRecord>(
                context,
                resource,
                MasterDataOperation.View,
                "persistence_unavailable",
                cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<CustomerRecord>> CreateCustomerAsync(
        MasterDataRequestContext context,
        CreateCustomerCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);

        var customerId = Guid.NewGuid();
        var resource = Resource(context, customerId, "customer-create");
        string code;
        LocalizedName? tradingName;
        IReadOnlyList<CustomerContactCommand> contacts;
        try
        {
            code = CustomerValuePolicy.NormalizeCode(command.Code);
            CustomerValuePolicy.ValidateName(command.LegalName, nameof(command.LegalName));
            tradingName = CustomerValuePolicy.NormalizeTradingName(command.TradingName);
            contacts = CustomerValuePolicy.NormalizeContacts(command.Contacts);
        }
        catch (ArgumentException)
        {
            return await FailedAsync<CustomerRecord>(
                context,
                resource,
                MasterDataOperation.Create,
                "validation_failed",
                cancellationToken);
        }

        resource = Resource(context, customerId, code);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.Create);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<CustomerRecord>(
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
            Contacts = contacts
        };
        var crossRoleReview = EvaluateCrossRoleReview(context, normalized);
        var evidence = CreateEvidence(
            context,
            resource,
            MasterDataOperation.Create,
            authorized.Decision,
            FoundationAuditReason.Allowed,
            afterSummary: CustomerSummary(normalized, MasterDataLifecycleState.Active, crossRoleReview));

        try
        {
            var result = await persistence.CreateCustomerAsync(
                context.TenantContext,
                customerId,
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
            return await FailedAsync<CustomerRecord>(
                context,
                resource,
                MasterDataOperation.Create,
                "persistence_unavailable",
                cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<CustomerRecord>> EditCustomerAsync(
        MasterDataRequestContext context,
        EditCustomerCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);

        var resource = Resource(
            context,
            command.CustomerId == Guid.Empty ? Guid.NewGuid() : command.CustomerId,
            "customer-edit");
        string code;
        LocalizedName? tradingName;
        IReadOnlyList<CustomerContactCommand> contacts;
        try
        {
            if (command.CustomerId == Guid.Empty)
            {
                throw new ArgumentException("A Business Customer identifier is required.", nameof(command));
            }

            ValidateVersion(command.ExpectedVersion);
            code = CustomerValuePolicy.NormalizeCode(command.Code);
            CustomerValuePolicy.ValidateName(command.LegalName, nameof(command.LegalName));
            tradingName = CustomerValuePolicy.NormalizeTradingName(command.TradingName);
            contacts = CustomerValuePolicy.NormalizeContacts(command.Contacts);
        }
        catch (ArgumentException)
        {
            return await FailedAsync<CustomerRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                "validation_failed",
                cancellationToken);
        }

        resource = Resource(context, command.CustomerId, code);
        var authorized = authorization.Authorize(context, resource, MasterDataOperation.Edit);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<CustomerRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                authorized.Decision,
                authorized.Code,
                cancellationToken);
        }

        CustomerRecord current;
        try
        {
            current = await persistence.FindCustomerAsync(
                context.TenantContext,
                command.CustomerId,
                cancellationToken) ?? throw new CustomerNotFoundException();
        }
        catch (CustomerNotFoundException)
        {
            return await FailedAsync<CustomerRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                "customer_not_found",
                cancellationToken);
        }
        catch
        {
            return await FailedAsync<CustomerRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                "persistence_unavailable",
                cancellationToken);
        }

        if (current.TenantId.Value != context.TenantId.Value)
        {
            return await FailedAsync<CustomerRecord>(
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
            Contacts = contacts
        };
        var crossRoleReview = EvaluateCrossRoleReview(context, normalized);
        var evidence = CreateEvidence(
            context,
            resource,
            MasterDataOperation.Edit,
            authorized.Decision,
            FoundationAuditReason.Allowed,
            beforeSummary: CustomerSummary(current),
            afterSummary: CustomerSummary(normalized, current.LifecycleState, crossRoleReview));

        try
        {
            var result = await persistence.EditCustomerAsync(
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
            return await FailedAsync<CustomerRecord>(
                context,
                resource,
                MasterDataOperation.Edit,
                "persistence_unavailable",
                cancellationToken);
        }
    }

    public Task<MasterDataOperationResult<CustomerRecord>> ReactivateCustomerAsync(
        MasterDataRequestContext context,
        Guid customerId,
        byte[] expectedVersion,
        CancellationToken cancellationToken = default) =>
        SetCustomerLifecycleAsync(
            context,
            customerId,
            MasterDataLifecycleState.Active,
            expectedVersion,
            reason: null,
            MasterDataOperation.Reactivate,
            cancellationToken);

    public Task<MasterDataOperationResult<CustomerRecord>> DeactivateCustomerAsync(
        MasterDataRequestContext context,
        Guid customerId,
        byte[] expectedVersion,
        string? reason,
        CancellationToken cancellationToken = default) =>
        SetCustomerLifecycleAsync(
            context,
            customerId,
            MasterDataLifecycleState.Inactive,
            expectedVersion,
            reason,
            MasterDataOperation.Deactivate,
            cancellationToken);

    public async Task<MasterDataOperationResult<CustomerRecord>> SetCustomerLifecycleAsync(
        MasterDataRequestContext context,
        Guid customerId,
        MasterDataLifecycleState lifecycleState,
        byte[] expectedVersion,
        string? reason,
        MasterDataOperation operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var resource = Resource(
            context,
            customerId == Guid.Empty ? Guid.NewGuid() : customerId,
            businessCode: null);
        if (customerId == Guid.Empty
            || lifecycleState is not (MasterDataLifecycleState.Active or MasterDataLifecycleState.Inactive)
            || operation is not (MasterDataOperation.Deactivate or MasterDataOperation.Reactivate)
            || operation == MasterDataOperation.Deactivate && lifecycleState != MasterDataLifecycleState.Inactive
            || operation == MasterDataOperation.Reactivate && lifecycleState != MasterDataLifecycleState.Active)
        {
            return await FailedAsync<CustomerRecord>(
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
                return await FailedAsync<CustomerRecord>(
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
            return await FailedAsync<CustomerRecord>(
                context,
                resource,
                operation,
                "validation_failed",
                cancellationToken);
        }

        var authorized = authorization.Authorize(context, resource, operation);
        if (!authorized.Allowed)
        {
            return await DeniedAsync<CustomerRecord>(
                context,
                resource,
                operation,
                authorized.Decision,
                authorized.Code,
                cancellationToken);
        }

        CustomerRecord current;
        try
        {
            current = await persistence.FindCustomerAsync(
                context.TenantContext,
                customerId,
                cancellationToken) ?? throw new CustomerNotFoundException();
        }
        catch (CustomerNotFoundException)
        {
            return await FailedAsync<CustomerRecord>(
                context,
                resource,
                operation,
                "customer_not_found",
                cancellationToken);
        }
        catch
        {
            return await FailedAsync<CustomerRecord>(
                context,
                resource,
                operation,
                "persistence_unavailable",
                cancellationToken);
        }

        if (current.TenantId.Value != context.TenantId.Value)
        {
            return await FailedAsync<CustomerRecord>(
                context,
                resource,
                operation,
                "cross_tenant_target_denied",
                cancellationToken);
        }

        if (current.LifecycleState == lifecycleState)
        {
            return await FailedAsync<CustomerRecord>(
                context,
                resource,
                operation,
                "customer_lifecycle_no_change",
                cancellationToken);
        }

        var afterSummary = $"{CustomerSummary(current with { LifecycleState = lifecycleState })};reason={AuditValue(normalizedReason)}";
        var evidence = CreateEvidence(
            context,
            resource,
            operation,
            authorized.Decision,
            FoundationAuditReason.Allowed,
            beforeSummary: CustomerSummary(current),
            afterSummary: afterSummary);

        try
        {
            var result = await persistence.SetCustomerLifecycleAsync(
                context.TenantContext,
                customerId,
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
            return await FailedAsync<CustomerRecord>(
                context,
                resource,
                operation,
                "persistence_unavailable",
                cancellationToken);
        }
    }

    public async Task<MasterDataOperationResult<IReadOnlyList<MasterDataAuditRecord>>> ReadAuditHistoryAsync(
        MasterDataRequestContext context,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var resource = Resource(
            context,
            customerId == Guid.Empty ? Guid.NewGuid() : customerId,
            businessCode: null);
        if (customerId == Guid.Empty)
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
                customerId,
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
        CreateCustomerCommand command)
    {
        try
        {
            return crossRoleMatchPolicy.Evaluate(
                context,
                CustomerValuePolicy.NameKey(command.LegalName.English)!,
                registrationKey: null);
        }
        catch
        {
            return SupplierCrossRoleMatchReview.Unavailable();
        }
    }

    private SupplierCrossRoleMatchReview EvaluateCrossRoleReview(
        MasterDataRequestContext context,
        EditCustomerCommand command)
    {
        try
        {
            return crossRoleMatchPolicy.Evaluate(
                context,
                CustomerValuePolicy.NameKey(command.LegalName.English)!,
                registrationKey: null);
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
        MasterDataResourceKind.BusinessCustomer,
        new TenantOwnership(context.TenantId.Value),
        stableId,
        businessCode,
        CustomerScopePolicy.CreateScope(context.TenantId));

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

    private static string CustomerSummary(CustomerRecord record) =>
        $"code={record.Code};legal-en={AuditValue(record.LegalName.English)};legal-ar={AuditValue(record.LegalName.Arabic)};"
        + $"trading-en={AuditValue(record.TradingName?.English)};trading-ar={AuditValue(record.TradingName?.Arabic)};"
        + $"contacts={record.Contacts.Count};state={record.LifecycleState}";

    private static string CustomerSummary(
        CreateCustomerCommand command,
        MasterDataLifecycleState lifecycleState,
        SupplierCrossRoleMatchReview review) =>
        $"code={command.Code};legal-en={AuditValue(command.LegalName.English)};legal-ar={AuditValue(command.LegalName.Arabic)};"
        + $"trading-en={AuditValue(command.TradingName?.English)};trading-ar={AuditValue(command.TradingName?.Arabic)};"
        + $"contacts={command.Contacts.Count};state={lifecycleState};"
        + $"cross-role-review={review.Code};review-count={review.EvidenceCount}";

    private static string CustomerSummary(
        EditCustomerCommand command,
        MasterDataLifecycleState lifecycleState,
        SupplierCrossRoleMatchReview review) =>
        CustomerSummary(
            new CreateCustomerCommand(
                command.Code,
                command.LegalName,
                command.TradingName,
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
        "customer_not_found" => FoundationAuditReason.NotFound,
        "concurrency_conflict" => FoundationAuditReason.ConcurrencyConflict,
        "customer_lifecycle_no_change"
            or "deactivation_reason_required" => FoundationAuditReason.LifecycleDenied,
        "validation_failed"
            or "customer_duplicate"
            or "customer_code_duplicate" => FoundationAuditReason.ValidationFailed,
        "permission_unavailable"
            or "scope_policy_unavailable"
            or "approval_policy_unavailable"
            or "resource_policy_unavailable"
            or "authorization_operation_unmapped"
            or "persistence_unavailable"
            or "customer_persistence_conflict"
            or "audit_unavailable"
            or "audit_evidence_invalid"
            or "audit_evidence_unavailable"
            or "cross_role_review_unavailable" => FoundationAuditReason.InternalFailure,
        _ => FoundationAuditReason.InternalFailure
    };

    private sealed class CustomerNotFoundException : Exception;
}

#pragma warning restore CS1591
