#pragma warning disable CS1591

using MiniErp.Contracts.Modules.Finance;

namespace MiniErp.App.Modules.Finance;

public interface IFinanceMesp134Persistence
{
    Task<IReadOnlyList<FinanceMonetaryPolicyRecord>> ListMonetaryPoliciesAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceMonetaryPolicyRecord>> CreateMonetaryPolicyAsync(FinanceRequestContext context, FinanceMonetaryPolicyCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceTaxAccountingEffectRecord>> ListTaxEffectsAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default);
    Task<FinanceTaxAccountingEffectRecord?> PreviewTaxAsync(FinanceRequestContext context, FinanceTaxAccountingCommand command, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceTaxAccountingEffectRecord>> PostTaxAsync(FinanceRequestContext context, FinanceTaxAccountingCommand command, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceTaxAccountingEffectRecord>> ReverseTaxAsync(FinanceRequestContext context, FinanceTaxAccountingReversalCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceRevaluationBatchRecord>> ListRevaluationBatchesAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default);
    Task<FinanceRevaluationBatchRecord?> GetRevaluationBatchAsync(FinanceRequestContext context, Guid batchId, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceRevaluationBatchRecord>> CreateRevaluationBatchAsync(FinanceRequestContext context, FinanceRevaluationBatchCommand command, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceRevaluationBatchRecord>> CalculateRevaluationBatchAsync(FinanceRequestContext context, FinanceRevaluationActionCommand command, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceRevaluationScopeEvaluation>> EvaluateRevaluationScopeAsync(FinanceRequestContext context, Guid companyId, DateOnly asOfDate, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceRevaluationBatchRecord>> PostRevaluationBatchAsync(FinanceRequestContext context, FinanceRevaluationActionCommand command, CancellationToken cancellationToken = default);
    Task<FinanceOperationResult<FinanceRevaluationBatchRecord>> ReverseRevaluationBatchAsync(FinanceRequestContext context, FinanceRevaluationActionCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceTaxAccountingReconciliationRecord>> ReconcileTaxAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceTaxAccountingReconciliationRecord>> ReconcileTaxAsync(FinanceRequestContext context, Guid companyId, DateOnly? asOfDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceFxReconciliationRecord>> ReconcileFxAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceFxReconciliationRecord>> ReconcileFxAsync(FinanceRequestContext context, Guid companyId, DateOnly? asOfDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceUnrealizedFxReconciliationRecord>> ReconcileUnrealizedFxAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceUnrealizedFxReconciliationRecord>> ReconcileUnrealizedFxAsync(FinanceRequestContext context, Guid companyId, DateOnly? asOfDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceReportingCurrencyReconciliationRecord>> ReconcileReportingCurrencyAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceReportingCurrencyReconciliationRecord>> ReconcileReportingCurrencyAsync(FinanceRequestContext context, Guid companyId, DateOnly? asOfDate, CancellationToken cancellationToken = default);
}

public sealed class UnavailableFinanceMesp134Persistence : IFinanceMesp134Persistence
{
    private static Task<T> Empty<T>() => Task.FromResult<T>(default!);
    private static Task<IReadOnlyList<T>> EmptyList<T>() => Task.FromResult<IReadOnlyList<T>>([]);
    private static FinanceOperationResult<T> Failure<T>() => FinanceOperationResult<T>.Failure("finance_unavailable");
    public Task<IReadOnlyList<FinanceMonetaryPolicyRecord>> ListMonetaryPoliciesAsync(FinanceRequestContext c, Guid x, CancellationToken t = default) => EmptyList<FinanceMonetaryPolicyRecord>();
    public Task<FinanceOperationResult<FinanceMonetaryPolicyRecord>> CreateMonetaryPolicyAsync(FinanceRequestContext c, FinanceMonetaryPolicyCommand x, CancellationToken t = default) => Task.FromResult(Failure<FinanceMonetaryPolicyRecord>());
    public Task<IReadOnlyList<FinanceTaxAccountingEffectRecord>> ListTaxEffectsAsync(FinanceRequestContext c, Guid x, CancellationToken t = default) => EmptyList<FinanceTaxAccountingEffectRecord>();
    public Task<FinanceTaxAccountingEffectRecord?> PreviewTaxAsync(FinanceRequestContext c, FinanceTaxAccountingCommand x, CancellationToken t = default) => Empty<FinanceTaxAccountingEffectRecord?>();
    public Task<FinanceOperationResult<FinanceTaxAccountingEffectRecord>> PostTaxAsync(FinanceRequestContext c, FinanceTaxAccountingCommand x, CancellationToken t = default) => Task.FromResult(Failure<FinanceTaxAccountingEffectRecord>());
    public Task<FinanceOperationResult<FinanceTaxAccountingEffectRecord>> ReverseTaxAsync(FinanceRequestContext c, FinanceTaxAccountingReversalCommand x, CancellationToken t = default) => Task.FromResult(Failure<FinanceTaxAccountingEffectRecord>());
    public Task<IReadOnlyList<FinanceRevaluationBatchRecord>> ListRevaluationBatchesAsync(FinanceRequestContext c, Guid x, CancellationToken t = default) => EmptyList<FinanceRevaluationBatchRecord>();
    public Task<FinanceRevaluationBatchRecord?> GetRevaluationBatchAsync(FinanceRequestContext c, Guid x, CancellationToken t = default) => Empty<FinanceRevaluationBatchRecord?>();
    public Task<FinanceOperationResult<FinanceRevaluationBatchRecord>> CreateRevaluationBatchAsync(FinanceRequestContext c, FinanceRevaluationBatchCommand x, CancellationToken t = default) => Task.FromResult(Failure<FinanceRevaluationBatchRecord>());
    public Task<FinanceOperationResult<FinanceRevaluationBatchRecord>> CalculateRevaluationBatchAsync(FinanceRequestContext c, FinanceRevaluationActionCommand x, CancellationToken t = default) => Task.FromResult(Failure<FinanceRevaluationBatchRecord>());
    public Task<FinanceOperationResult<FinanceRevaluationScopeEvaluation>> EvaluateRevaluationScopeAsync(FinanceRequestContext c, Guid x, DateOnly d, CancellationToken t = default) => Task.FromResult(Failure<FinanceRevaluationScopeEvaluation>());
    public Task<FinanceOperationResult<FinanceRevaluationBatchRecord>> PostRevaluationBatchAsync(FinanceRequestContext c, FinanceRevaluationActionCommand x, CancellationToken t = default) => Task.FromResult(Failure<FinanceRevaluationBatchRecord>());
    public Task<FinanceOperationResult<FinanceRevaluationBatchRecord>> ReverseRevaluationBatchAsync(FinanceRequestContext c, FinanceRevaluationActionCommand x, CancellationToken t = default) => Task.FromResult(Failure<FinanceRevaluationBatchRecord>());
    public Task<IReadOnlyList<FinanceTaxAccountingReconciliationRecord>> ReconcileTaxAsync(FinanceRequestContext c, Guid x, CancellationToken t = default) => EmptyList<FinanceTaxAccountingReconciliationRecord>();
    public Task<IReadOnlyList<FinanceTaxAccountingReconciliationRecord>> ReconcileTaxAsync(FinanceRequestContext c, Guid x, DateOnly? d, CancellationToken t = default) => EmptyList<FinanceTaxAccountingReconciliationRecord>();
    public Task<IReadOnlyList<FinanceFxReconciliationRecord>> ReconcileFxAsync(FinanceRequestContext c, Guid x, CancellationToken t = default) => EmptyList<FinanceFxReconciliationRecord>();
    public Task<IReadOnlyList<FinanceFxReconciliationRecord>> ReconcileFxAsync(FinanceRequestContext c, Guid x, DateOnly? d, CancellationToken t = default) => EmptyList<FinanceFxReconciliationRecord>();
    public Task<IReadOnlyList<FinanceUnrealizedFxReconciliationRecord>> ReconcileUnrealizedFxAsync(FinanceRequestContext c, Guid x, CancellationToken t = default) => EmptyList<FinanceUnrealizedFxReconciliationRecord>();
    public Task<IReadOnlyList<FinanceUnrealizedFxReconciliationRecord>> ReconcileUnrealizedFxAsync(FinanceRequestContext c, Guid x, DateOnly? d, CancellationToken t = default) => EmptyList<FinanceUnrealizedFxReconciliationRecord>();
    public Task<IReadOnlyList<FinanceReportingCurrencyReconciliationRecord>> ReconcileReportingCurrencyAsync(FinanceRequestContext c, Guid x, CancellationToken t = default) => EmptyList<FinanceReportingCurrencyReconciliationRecord>();
    public Task<IReadOnlyList<FinanceReportingCurrencyReconciliationRecord>> ReconcileReportingCurrencyAsync(FinanceRequestContext c, Guid x, DateOnly? d, CancellationToken t = default) => EmptyList<FinanceReportingCurrencyReconciliationRecord>();
}

#pragma warning restore CS1591
