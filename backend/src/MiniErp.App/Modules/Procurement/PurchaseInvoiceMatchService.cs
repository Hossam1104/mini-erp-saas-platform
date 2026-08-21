#pragma warning disable CS1591

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.App.Modules.Procurement;

public sealed record PurchaseInvoiceMatchOperationResult<T>(bool Succeeded, string Code, T? Value)
{
    public static PurchaseInvoiceMatchOperationResult<T> Success(T value) => new(true, "succeeded", value);
    public static PurchaseInvoiceMatchOperationResult<T> Failure(string code) => new(false, code, default);
}

public sealed class PurchaseInvoiceMatchService
{
    private const string ListOperationId = "procurement.matching.list";
    private const string ReadOperationId = "procurement.matching.read";
    private const string EvaluateOperationId = "procurement.matching.evaluate";
    private const string ResolveOperationId = "procurement.matching.resolve-exception";
    private const string HistoryOperationId = "procurement.matching.history.read";
    private const string AuditOperationId = "procurement.matching.audit.read";
    private readonly PurchaseRequestAuthorizationService authorization;
    private readonly IPurchaseInvoiceHandoffPersistence handoffPersistence;
    private readonly IPurchaseInvoiceMatchPersistence persistence;
    private readonly IPurchaseInvoiceMatchingTolerancePolicyProvider tolerancePolicies;
    private readonly IPurchaseInvoiceMatchingResolutionPolicyProvider resolutionPolicies;
    private readonly IPurchaseInvoiceMatchingExchangeRateReferenceProvider exchangeRates;

    public PurchaseInvoiceMatchService(
        PurchaseRequestAuthorizationService authorization,
        IPurchaseInvoiceHandoffPersistence handoffPersistence,
        IPurchaseInvoiceMatchPersistence persistence,
        IPurchaseInvoiceMatchingTolerancePolicyProvider tolerancePolicies,
        IPurchaseInvoiceMatchingResolutionPolicyProvider resolutionPolicies,
        IPurchaseInvoiceMatchingExchangeRateReferenceProvider exchangeRates)
    {
        this.authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        this.handoffPersistence = handoffPersistence ?? throw new ArgumentNullException(nameof(handoffPersistence));
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        this.tolerancePolicies = tolerancePolicies ?? throw new ArgumentNullException(nameof(tolerancePolicies));
        this.resolutionPolicies = resolutionPolicies ?? throw new ArgumentNullException(nameof(resolutionPolicies));
        this.exchangeRates = exchangeRates ?? throw new ArgumentNullException(nameof(exchangeRates));
    }

    public async Task<PurchaseInvoiceMatchOperationResult<IReadOnlyList<PurchaseInvoiceMatchListRecord>>> ListAsync(
        ProcurementRequestContext context,
        Guid? handoffId,
        PurchaseInvoiceMatchResult? result,
        CancellationToken cancellationToken = default)
    {
        var authorized = authorization.Authorize(context, ListOperationId);
        if (!authorized.Allowed) return PurchaseInvoiceMatchOperationResult<IReadOnlyList<PurchaseInvoiceMatchListRecord>>.Failure(authorized.Code);
        try
        {
            var records = await persistence.ListAsync(context.TenantContext, handoffId, result, cancellationToken);
            return PurchaseInvoiceMatchOperationResult<IReadOnlyList<PurchaseInvoiceMatchListRecord>>.Success(records.Where(item => authorization.Authorize(context, ListOperationId, item.Scope).Allowed).ToArray());
        }
        catch { return PurchaseInvoiceMatchOperationResult<IReadOnlyList<PurchaseInvoiceMatchListRecord>>.Failure("persistence_unavailable"); }
    }

    public async Task<PurchaseInvoiceMatchOperationResult<PurchaseInvoiceMatchRecord>> GetAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        var record = await FindAsync(context, id, cancellationToken);
        if (!record.Succeeded || record.Value is null) return record;
        var authorized = authorization.Authorize(context, ReadOperationId, record.Value.Scope);
        return authorized.Allowed ? record : PurchaseInvoiceMatchOperationResult<PurchaseInvoiceMatchRecord>.Failure(authorized.Code);
    }

    public async Task<PurchaseInvoiceMatchOperationResult<PurchaseInvoiceMatchRecord>> EvaluateAsync(
        ProcurementRequestContext context,
        Guid handoffId,
        byte[] expectedHandoffVersion,
        PurchaseInvoiceMatchEvaluateRequest request,
        string? idempotencyKey,
        string? requestFingerprint,
        CancellationToken cancellationToken = default)
    {
        var handoff = await FindHandoffAsync(context, handoffId, EvaluateOperationId, cancellationToken);
        if (!handoff.Succeeded || handoff.Value is null) return PurchaseInvoiceMatchOperationResult<PurchaseInvoiceMatchRecord>.Failure(handoff.Code);
        if (expectedHandoffVersion is null || expectedHandoffVersion.Length == 0) return PurchaseInvoiceMatchOperationResult<PurchaseInvoiceMatchRecord>.Failure("validation_failed");
        if (request is null) return PurchaseInvoiceMatchOperationResult<PurchaseInvoiceMatchRecord>.Failure("validation_failed");

        var occurredAt = DateTimeOffset.UtcNow;
        var policy = await tolerancePolicies.ResolveAsync(handoff.Value.Scope, occurredAt, cancellationToken);
        if (!IsValidPolicy(policy)) return PurchaseInvoiceMatchOperationResult<PurchaseInvoiceMatchRecord>.Failure("matching_policy_invalid");

        PurchaseInvoiceMatchExchangeRateRecord? exchangeRate = null;
        if (request.ExchangeRateReference is { } exchangeRateReference
            && handoff.Value.DeclaredEvidence is { } declaredEvidence)
        {
            if (string.Equals(declaredEvidence.CurrencyCode, handoff.Value.CurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                return PurchaseInvoiceMatchOperationResult<PurchaseInvoiceMatchRecord>.Failure("exchange_rate_invalid");
            }

            var resolution = await exchangeRates.ResolveAsync(
                context.TenantContext,
                exchangeRateReference.ExchangeRateId,
                declaredEvidence.CurrencyCode,
                handoff.Value.CurrencyCode,
                declaredEvidence.SupplierInvoiceDate ?? handoff.Value.SupplierInvoiceDate,
                cancellationToken);
            // An unresolved reference deliberately remains null so persistence
            // records CurrencyNotComparable/NotMatchReady. No client value is
            // ever used as a fallback.
            exchangeRate = resolution.Value;
        }

        var matchId = Guid.NewGuid();
        var command = new PurchaseInvoiceMatchEvaluateCommand(handoffId, expectedHandoffVersion, context.ActorId, policy, exchangeRate, occurredAt, Correlation(context), idempotencyKey, Fingerprint(EvaluateOperationId, handoffId, requestFingerprint));
        var evidence = new PurchaseInvoiceMatchAuditEvidence(matchId, matchId, handoffId, context.TenantId.Value, context.ActorId, EvaluateOperationId, Correlation(context), "Allowed", null, idempotencyKey, command.RequestFingerprint, occurredAt);
        return ToOperationResult(await persistence.EvaluateAsync(context.TenantContext, command, evidence, cancellationToken));
    }

    public async Task<PurchaseInvoiceMatchOperationResult<PurchaseInvoiceMatchRecord>> ResolveAsync(
        ProcurementRequestContext context,
        Guid matchId,
        byte[] expectedMatchVersion,
        PurchaseInvoiceMatchResolveRequest request,
        string? idempotencyKey,
        string? requestFingerprint,
        CancellationToken cancellationToken = default)
    {
        var current = await FindAsync(context, matchId, cancellationToken);
        if (!current.Succeeded || current.Value is null) return current;
        var authorized = authorization.Authorize(context, ResolveOperationId, current.Value.Scope);
        if (!authorized.Allowed) return PurchaseInvoiceMatchOperationResult<PurchaseInvoiceMatchRecord>.Failure(authorized.Code);
        if (expectedMatchVersion is null || expectedMatchVersion.Length == 0) return PurchaseInvoiceMatchOperationResult<PurchaseInvoiceMatchRecord>.Failure("validation_failed");
        if (request is null || !PurchaseInvoiceHandoffValuePolicy.TryReason(request.Reason, out var reason)) return PurchaseInvoiceMatchOperationResult<PurchaseInvoiceMatchRecord>.Failure("reason_required");
        var occurredAt = DateTimeOffset.UtcNow;
        var policy = await resolutionPolicies.ResolveAsync(current.Value.Scope, occurredAt, cancellationToken);
        if (!policy.AllowResolution) return PurchaseInvoiceMatchOperationResult<PurchaseInvoiceMatchRecord>.Failure("resolution_policy_denied");
        if (policy.RequireReason && string.IsNullOrWhiteSpace(reason)) return PurchaseInvoiceMatchOperationResult<PurchaseInvoiceMatchRecord>.Failure("reason_required");
        if (policy.RequireDifferentActor && current.Value.EvaluatedByActorId == context.ActorId) return PurchaseInvoiceMatchOperationResult<PurchaseInvoiceMatchRecord>.Failure("sod_violation");

        var fingerprint = Fingerprint(ResolveOperationId, matchId, requestFingerprint);
        var command = new PurchaseInvoiceMatchResolveCommand(matchId, expectedMatchVersion, context.ActorId, policy, reason, occurredAt, Correlation(context), idempotencyKey, fingerprint);
        var evidence = new PurchaseInvoiceMatchAuditEvidence(Guid.NewGuid(), matchId, current.Value.PurchaseInvoiceHandoffId, context.TenantId.Value, context.ActorId, ResolveOperationId, Correlation(context), "Allowed", reason, idempotencyKey, fingerprint, occurredAt);
        return ToOperationResult(await persistence.ResolveAsync(context.TenantContext, command, evidence, cancellationToken));
    }

    public async Task<PurchaseInvoiceMatchOperationResult<IReadOnlyList<PurchaseInvoiceMatchHistoryRecord>>> ReadHistoryAsync(ProcurementRequestContext context, Guid matchId, CancellationToken cancellationToken = default)
    {
        var current = await GetAsync(context, matchId, cancellationToken);
        if (!current.Succeeded || current.Value is null) return PurchaseInvoiceMatchOperationResult<IReadOnlyList<PurchaseInvoiceMatchHistoryRecord>>.Failure(current.Code);
        var authorized = authorization.Authorize(context, HistoryOperationId, current.Value.Scope);
        if (!authorized.Allowed) return PurchaseInvoiceMatchOperationResult<IReadOnlyList<PurchaseInvoiceMatchHistoryRecord>>.Failure(authorized.Code);
        try { return PurchaseInvoiceMatchOperationResult<IReadOnlyList<PurchaseInvoiceMatchHistoryRecord>>.Success(await persistence.ReadHistoryAsync(context.TenantContext, matchId, cancellationToken)); }
        catch { return PurchaseInvoiceMatchOperationResult<IReadOnlyList<PurchaseInvoiceMatchHistoryRecord>>.Failure("persistence_unavailable"); }
    }

    public async Task<PurchaseInvoiceMatchOperationResult<IReadOnlyList<PurchaseInvoiceMatchAuditRecord>>> ReadAuditAsync(ProcurementRequestContext context, Guid matchId, CancellationToken cancellationToken = default)
    {
        var current = await GetAsync(context, matchId, cancellationToken);
        if (!current.Succeeded || current.Value is null) return PurchaseInvoiceMatchOperationResult<IReadOnlyList<PurchaseInvoiceMatchAuditRecord>>.Failure(current.Code);
        var authorized = authorization.Authorize(context, AuditOperationId, current.Value.Scope);
        if (!authorized.Allowed) return PurchaseInvoiceMatchOperationResult<IReadOnlyList<PurchaseInvoiceMatchAuditRecord>>.Failure(authorized.Code);
        try { return PurchaseInvoiceMatchOperationResult<IReadOnlyList<PurchaseInvoiceMatchAuditRecord>>.Success(await persistence.ReadAuditAsync(context.TenantContext, matchId, cancellationToken)); }
        catch { return PurchaseInvoiceMatchOperationResult<IReadOnlyList<PurchaseInvoiceMatchAuditRecord>>.Failure("persistence_unavailable"); }
    }

    private async Task<PurchaseInvoiceMatchOperationResult<PurchaseInvoiceMatchRecord>> FindAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return PurchaseInvoiceMatchOperationResult<PurchaseInvoiceMatchRecord>.Failure("validation_failed");
        try
        {
            var record = await persistence.FindAsync(context.TenantContext, id, cancellationToken);
            return record is null ? PurchaseInvoiceMatchOperationResult<PurchaseInvoiceMatchRecord>.Failure("match_evaluation_not_found") : PurchaseInvoiceMatchOperationResult<PurchaseInvoiceMatchRecord>.Success(record);
        }
        catch { return PurchaseInvoiceMatchOperationResult<PurchaseInvoiceMatchRecord>.Failure("persistence_unavailable"); }
    }

    private async Task<PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>> FindHandoffAsync(ProcurementRequestContext context, Guid id, string operationId, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>.Failure("validation_failed");
        try
        {
            var record = await handoffPersistence.FindAsync(context.TenantContext, id, cancellationToken);
            if (record is null) return PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>.Failure("invoice_handoff_not_found");
            var authorized = authorization.Authorize(context, operationId, record.Scope);
            return authorized.Allowed
                ? PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>.Success(record)
                : PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>.Failure(authorized.Code);
        }
        catch
        {
            return PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>.Failure("persistence_unavailable");
        }
    }

    private static bool IsValidPolicy(PurchaseInvoiceMatchingToleranceDefinition policy) =>
        policy is not null && policy.Version > 0 && policy.PolicyId.Length is > 0 and <= 128
        && new[] { policy.QuantityAbsoluteTolerance, policy.QuantityPercentageTolerance, policy.PriceAbsoluteTolerance, policy.PricePercentageTolerance, policy.AmountAbsoluteTolerance, policy.AmountPercentageTolerance, policy.TaxAbsoluteTolerance, policy.TaxPercentageTolerance }.All(item => item >= 0m);

    private static string Correlation(ProcurementRequestContext context) => context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N");
    private static string Fingerprint(string operationId, Guid target, string? raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{operationId}|{target:D}|{raw}")));
    private static PurchaseInvoiceMatchOperationResult<T> ToOperationResult<T>(PurchaseInvoiceMatchPersistenceResult<T> result) => result.Succeeded && result.Value is not null ? PurchaseInvoiceMatchOperationResult<T>.Success(result.Value) : PurchaseInvoiceMatchOperationResult<T>.Failure(result.Code);

}

#pragma warning restore CS1591
