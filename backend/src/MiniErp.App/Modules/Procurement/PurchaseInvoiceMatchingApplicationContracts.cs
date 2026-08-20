#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.App.Modules.Procurement;

public sealed record PurchaseInvoiceMatchingToleranceDefinition(
    string PolicyId,
    int Version,
    decimal QuantityAbsoluteTolerance,
    decimal QuantityPercentageTolerance,
    decimal PriceAbsoluteTolerance,
    decimal PricePercentageTolerance,
    decimal AmountAbsoluteTolerance,
    decimal AmountPercentageTolerance,
    decimal TaxAbsoluteTolerance,
    decimal TaxPercentageTolerance,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo)
{
    public static PurchaseInvoiceMatchingToleranceDefinition ExactSafe(DateTimeOffset now) => new(
        "exact-safe-default", 1, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, DateTimeOffset.MinValue, null);

    public PurchaseInvoiceMatchPolicyResponse ToResponse() => new(
        PolicyId, Version, QuantityAbsoluteTolerance, QuantityPercentageTolerance,
        PriceAbsoluteTolerance, PricePercentageTolerance, AmountAbsoluteTolerance,
        AmountPercentageTolerance, TaxAbsoluteTolerance, TaxPercentageTolerance,
        EffectiveFrom, EffectiveTo);

    public bool IsEffective(DateTimeOffset at) => EffectiveFrom <= at && (EffectiveTo is null || at < EffectiveTo);
}

public sealed record PurchaseInvoiceMatchingToleranceBinding(PurchaseRequestScope Scope, PurchaseInvoiceMatchingToleranceDefinition Definition);

public interface IPurchaseInvoiceMatchingTolerancePolicyProvider
{
    Task<PurchaseInvoiceMatchingToleranceDefinition> ResolveAsync(PurchaseRequestScope scope, DateTimeOffset at, CancellationToken cancellationToken = default);
}

public sealed class ExactSafePurchaseInvoiceMatchingTolerancePolicyProvider : IPurchaseInvoiceMatchingTolerancePolicyProvider
{
    public Task<PurchaseInvoiceMatchingToleranceDefinition> ResolveAsync(PurchaseRequestScope scope, DateTimeOffset at, CancellationToken cancellationToken = default) =>
        Task.FromResult(PurchaseInvoiceMatchingToleranceDefinition.ExactSafe(at));
}

public sealed class ConfiguredPurchaseInvoiceMatchingTolerancePolicyProvider : IPurchaseInvoiceMatchingTolerancePolicyProvider
{
    private readonly IReadOnlyList<PurchaseInvoiceMatchingToleranceBinding> bindings;

    public ConfiguredPurchaseInvoiceMatchingTolerancePolicyProvider(IEnumerable<PurchaseInvoiceMatchingToleranceBinding> bindings)
    {
        this.bindings = bindings?.ToArray() ?? throw new ArgumentNullException(nameof(bindings));
    }

    public Task<PurchaseInvoiceMatchingToleranceDefinition> ResolveAsync(PurchaseRequestScope scope, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        var selected = bindings
            .Where(item => item.Scope.Matches(scope) && item.Definition.IsEffective(at))
            .OrderByDescending(item => item.Definition.Version)
            .ThenBy(item => item.Definition.PolicyId, StringComparer.Ordinal)
            .Select(item => item.Definition)
            .FirstOrDefault() ?? PurchaseInvoiceMatchingToleranceDefinition.ExactSafe(at);
        return Task.FromResult(selected);
    }
}

public sealed record PurchaseInvoiceMatchingResolutionPolicyDefinition(
    string PolicyId,
    int Version,
    bool AllowResolution,
    bool RequireDifferentActor,
    bool RequireReason,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo)
{
    public static PurchaseInvoiceMatchingResolutionPolicyDefinition Default(DateTimeOffset at) => new(
        "controlled-exception-resolution", 1, true, true, true, DateTimeOffset.MinValue, null);

    public bool IsEffective(DateTimeOffset at) => EffectiveFrom <= at && (EffectiveTo is null || at < EffectiveTo);

    public PurchaseInvoiceMatchResolutionPolicyResponse ToResponse() => new(
        PolicyId, Version, AllowResolution, RequireDifferentActor, RequireReason, EffectiveFrom, EffectiveTo);
}

public sealed record PurchaseInvoiceMatchingResolutionPolicyBinding(PurchaseRequestScope Scope, PurchaseInvoiceMatchingResolutionPolicyDefinition Definition);

public interface IPurchaseInvoiceMatchingResolutionPolicyProvider
{
    Task<PurchaseInvoiceMatchingResolutionPolicyDefinition> ResolveAsync(PurchaseRequestScope scope, DateTimeOffset at, CancellationToken cancellationToken = default);
}

public sealed class DefaultPurchaseInvoiceMatchingResolutionPolicyProvider : IPurchaseInvoiceMatchingResolutionPolicyProvider
{
    public Task<PurchaseInvoiceMatchingResolutionPolicyDefinition> ResolveAsync(PurchaseRequestScope scope, DateTimeOffset at, CancellationToken cancellationToken = default) =>
        Task.FromResult(PurchaseInvoiceMatchingResolutionPolicyDefinition.Default(at));
}

public sealed class ConfiguredPurchaseInvoiceMatchingResolutionPolicyProvider : IPurchaseInvoiceMatchingResolutionPolicyProvider
{
    private readonly IReadOnlyList<PurchaseInvoiceMatchingResolutionPolicyBinding> bindings;

    public ConfiguredPurchaseInvoiceMatchingResolutionPolicyProvider(IEnumerable<PurchaseInvoiceMatchingResolutionPolicyBinding> bindings)
    {
        this.bindings = bindings?.ToArray() ?? throw new ArgumentNullException(nameof(bindings));
    }

    public Task<PurchaseInvoiceMatchingResolutionPolicyDefinition> ResolveAsync(PurchaseRequestScope scope, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        var selected = bindings
            .Where(item => item.Scope.Matches(scope) && item.Definition.IsEffective(at))
            .OrderByDescending(item => item.Definition.Version)
            .ThenBy(item => item.Definition.PolicyId, StringComparer.Ordinal)
            .Select(item => item.Definition)
            .FirstOrDefault() ?? PurchaseInvoiceMatchingResolutionPolicyDefinition.Default(at);
        return Task.FromResult(selected);
    }
}

public sealed record PurchaseInvoiceMatchVarianceRecord(
    string Classification,
    Guid? PurchaseOrderLineId,
    Guid? GoodsReceiptLineId,
    decimal? ExpectedValue,
    decimal? ActualValue,
    decimal? Variance,
    decimal AllowedTolerance,
    string? CurrencyCode,
    string? Details)
{
    public PurchaseInvoiceMatchVarianceResponse ToResponse() => new(Classification, PurchaseOrderLineId, GoodsReceiptLineId, ExpectedValue, ActualValue, Variance, AllowedTolerance, CurrencyCode, Details);
}

public sealed record PurchaseInvoiceMatchExchangeRateRecord(
    string SourceCurrencyCode,
    string TargetCurrencyCode,
    decimal Rate,
    int Scale,
    string? Source,
    string? Version,
    DateOnly? EffectiveOn)
{
    public PurchaseInvoiceMatchExchangeRateResponse ToResponse() => new(SourceCurrencyCode, TargetCurrencyCode, Rate, Scale, Source, Version, EffectiveOn);
}

public sealed record PurchaseInvoiceMatchRecord(
    Guid Id,
    Guid TenantId,
    PurchaseRequestScope Scope,
    Guid PurchaseInvoiceHandoffId,
    Guid PurchaseOrderId,
    PurchaseInvoiceMatchLifecycle Lifecycle,
    PurchaseInvoiceMatchResult Result,
    DateTimeOffset EvaluatedAt,
    Guid EvaluatedByActorId,
    Guid? ResolvedByActorId,
    DateTimeOffset? ResolvedAt,
    string? ResolutionReason,
    string SourceFingerprint,
    byte[] PurchaseOrderVersion,
    byte[] HandoffVersion,
    Guid? DeclaredEvidenceId,
    int? DeclaredEvidenceVersion,
    PurchaseInvoiceMatchingToleranceDefinition Policy,
    PurchaseInvoiceMatchingResolutionPolicyDefinition? ResolutionPolicy,
    PurchaseInvoiceMatchExchangeRateRecord? AppliedExchangeRate,
    IReadOnlyList<PurchaseInvoiceMatchVarianceRecord> Variances,
    string? SourceSnapshot,
    byte[] Version);

public sealed record PurchaseInvoiceMatchListRecord(
    Guid Id,
    PurchaseRequestScope Scope,
    Guid PurchaseInvoiceHandoffId,
    Guid PurchaseOrderId,
    PurchaseInvoiceMatchLifecycle Lifecycle,
    PurchaseInvoiceMatchResult Result,
    DateTimeOffset EvaluatedAt,
    Guid? ResolvedByActorId,
    int VarianceCount,
    byte[] Version);

public sealed record PurchaseInvoiceMatchHistoryRecord(
    Guid Id,
    Guid MatchEvaluationId,
    Guid PurchaseInvoiceHandoffId,
    PurchaseInvoiceMatchResult Result,
    string Action,
    Guid ActorId,
    string? Reason,
    DateTimeOffset OccurredAt,
    string CorrelationId);

public sealed record PurchaseInvoiceMatchAuditRecord(
    Guid Id,
    Guid MatchEvaluationId,
    Guid PurchaseInvoiceHandoffId,
    string OperationId,
    Guid TenantId,
    Guid ActorId,
    string Decision,
    string? Reason,
    DateTimeOffset OccurredAt,
    string? IdempotencyKey,
    string? RequestFingerprint);

public sealed record PurchaseInvoiceMatchEvaluateCommand(
    Guid PurchaseInvoiceHandoffId,
    byte[] ExpectedHandoffVersion,
    Guid ActorId,
    PurchaseInvoiceMatchingToleranceDefinition Policy,
    PurchaseInvoiceMatchExchangeRateRecord? AppliedExchangeRate,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string? IdempotencyKey,
    string? RequestFingerprint);

public sealed record PurchaseInvoiceMatchResolveCommand(
    Guid MatchEvaluationId,
    byte[] ExpectedMatchVersion,
    Guid ActorId,
    PurchaseInvoiceMatchingResolutionPolicyDefinition Policy,
    string Reason,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string? IdempotencyKey,
    string? RequestFingerprint);

public sealed record PurchaseInvoiceMatchAuditEvidence(
    Guid Id,
    Guid MatchEvaluationId,
    Guid PurchaseInvoiceHandoffId,
    Guid TenantId,
    Guid ActorId,
    string OperationId,
    string CorrelationId,
    string Decision,
    string? Reason,
    string? IdempotencyKey,
    string? RequestFingerprint,
    DateTimeOffset OccurredAt);

public enum PurchaseInvoiceMatchPersistenceOutcome
{
    Succeeded = 1,
    NotFound = 2,
    Conflict = 3,
    InvalidState = 4,
    Duplicate = 5,
    Failure = 6
}

public sealed record PurchaseInvoiceMatchPersistenceResult<T>(PurchaseInvoiceMatchPersistenceOutcome Outcome, string Code, T? Value)
{
    public bool Succeeded => Outcome == PurchaseInvoiceMatchPersistenceOutcome.Succeeded;
    public static PurchaseInvoiceMatchPersistenceResult<T> Success(T value) => new(PurchaseInvoiceMatchPersistenceOutcome.Succeeded, "persisted", value);
    public static PurchaseInvoiceMatchPersistenceResult<T> Denied(PurchaseInvoiceMatchPersistenceOutcome outcome, string code) => new(outcome, code, default);
}

public interface IPurchaseInvoiceMatchPersistence
{
    Task<IReadOnlyList<PurchaseInvoiceMatchListRecord>> ListAsync(TenantContext tenantContext, Guid? handoffId, PurchaseInvoiceMatchResult? result, CancellationToken cancellationToken = default);
    Task<PurchaseInvoiceMatchRecord?> FindAsync(TenantContext tenantContext, Guid matchEvaluationId, CancellationToken cancellationToken = default);
    Task<PurchaseInvoiceMatchRecord?> FindCurrentForHandoffAsync(TenantContext tenantContext, Guid handoffId, CancellationToken cancellationToken = default);
    Task<PurchaseInvoiceMatchPersistenceResult<PurchaseInvoiceMatchRecord>> EvaluateAsync(TenantContext tenantContext, PurchaseInvoiceMatchEvaluateCommand command, PurchaseInvoiceMatchAuditEvidence evidence, CancellationToken cancellationToken = default);
    Task<PurchaseInvoiceMatchPersistenceResult<PurchaseInvoiceMatchRecord>> ResolveAsync(TenantContext tenantContext, PurchaseInvoiceMatchResolveCommand command, PurchaseInvoiceMatchAuditEvidence evidence, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchaseInvoiceMatchHistoryRecord>> ReadHistoryAsync(TenantContext tenantContext, Guid matchEvaluationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchaseInvoiceMatchAuditRecord>> ReadAuditAsync(TenantContext tenantContext, Guid matchEvaluationId, CancellationToken cancellationToken = default);
}

public sealed class UnavailablePurchaseInvoiceMatchPersistence : IPurchaseInvoiceMatchPersistence
{
    private static Task<PurchaseInvoiceMatchPersistenceResult<T>> Unavailable<T>() => Task.FromResult(PurchaseInvoiceMatchPersistenceResult<T>.Denied(PurchaseInvoiceMatchPersistenceOutcome.Failure, "persistence_unavailable"));
    public Task<IReadOnlyList<PurchaseInvoiceMatchListRecord>> ListAsync(TenantContext tenantContext, Guid? handoffId, PurchaseInvoiceMatchResult? result, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseInvoiceMatchListRecord>>([]);
    public Task<PurchaseInvoiceMatchRecord?> FindAsync(TenantContext tenantContext, Guid matchEvaluationId, CancellationToken cancellationToken = default) => Task.FromResult<PurchaseInvoiceMatchRecord?>(null);
    public Task<PurchaseInvoiceMatchRecord?> FindCurrentForHandoffAsync(TenantContext tenantContext, Guid handoffId, CancellationToken cancellationToken = default) => Task.FromResult<PurchaseInvoiceMatchRecord?>(null);
    public Task<PurchaseInvoiceMatchPersistenceResult<PurchaseInvoiceMatchRecord>> EvaluateAsync(TenantContext tenantContext, PurchaseInvoiceMatchEvaluateCommand command, PurchaseInvoiceMatchAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseInvoiceMatchRecord>();
    public Task<PurchaseInvoiceMatchPersistenceResult<PurchaseInvoiceMatchRecord>> ResolveAsync(TenantContext tenantContext, PurchaseInvoiceMatchResolveCommand command, PurchaseInvoiceMatchAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseInvoiceMatchRecord>();
    public Task<IReadOnlyList<PurchaseInvoiceMatchHistoryRecord>> ReadHistoryAsync(TenantContext tenantContext, Guid matchEvaluationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseInvoiceMatchHistoryRecord>>([]);
    public Task<IReadOnlyList<PurchaseInvoiceMatchAuditRecord>> ReadAuditAsync(TenantContext tenantContext, Guid matchEvaluationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseInvoiceMatchAuditRecord>>([]);
}

#pragma warning restore CS1591
