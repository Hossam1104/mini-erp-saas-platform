using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;

namespace MiniErp.ArchitectureTests;

/// <summary>
/// Test-only authority issuer. Production code has no root-scope factory; all
/// shipping scope issuance remains owned by the Identity resolver.
/// </summary>
internal static class DurableWorkTestSupport
{
    internal static TenantWorkScope TenantWideScope(TenantContext context) =>
        TenantWorkScope.IssueFromVerifiedAuthority(
            context,
            TenantWorkScopeRequest.TenantWide());

    internal static TenantWorkScope ScopeFor(
        TenantContext context,
        DurableWorkItem workItem) =>
        TenantWorkScope.IssueFromVerifiedAuthority(
            ExactContext(context, workItem),
            new TenantWorkScopeRequest(
                workItem.Scope.CompanyId,
                workItem.Scope.BranchId,
                workItem.Scope.WarehouseId));

    internal static DurableWorkAuthorityValidationResult Approve(
        DurableWorkItem workItem,
        TenantContext currentTenantContext)
    {
        var executionContext = ExactContext(currentTenantContext, workItem);
        var scope = TenantWorkScope.IssueFromVerifiedAuthority(
            executionContext,
            new TenantWorkScopeRequest(
                workItem.Scope.CompanyId,
                workItem.Scope.BranchId,
                workItem.Scope.WarehouseId));
        var authorization = new VerifiedDurableWorkAuthorization(
            workItem,
            workItem.Identity.WorkItemId,
            workItem.TenantId,
            workItem.Identity.CorrelationId,
            workItem.Identity.Operation,
            executionContext,
            scope,
            executionContext.ActorId!.Value,
            workItem.Initiator.SessionId!.Value);
        return DurableWorkAuthorityValidationResult.Approved(authorization);
    }

    /// <summary>
    /// Fabricates a <see cref="VerifiedDurableWorkReconciliationAuthorization"/>
    /// directly from a raw context for tests that only need the reconciliation
    /// read port to work end to end, without exercising the real Identity
    /// authorization decision path (see DurableWorkAuthorityRevalidationTests
    /// / the dedicated reconciliation-authorization tests for that).
    /// </summary>
    internal static VerifiedDurableWorkReconciliationAuthorization ApproveReconciliation(
        TenantContext context,
        TenantWorkScopeRequest? requestedScope = null)
    {
        var scopeRequest = requestedScope ?? TenantWorkScopeRequest.TenantWide();
        var scopeValue = scopeRequest.WarehouseId is { } warehouseId
            ? $"Warehouse:{warehouseId}"
            : scopeRequest.BranchId is { } branchId
                ? $"Branch:{branchId}"
                : scopeRequest.CompanyId is { } companyId
                    ? $"Company:{companyId}"
                    : $"Tenant:{context.TenantId.Value}";
        var exactScopeReference = new ScopeReference(scopeValue);
        var executionContext = context.AuthorizationPath switch
        {
            TenantAuthorizationPath.OrdinaryMembership when context.Membership is { } membership =>
                TenantContext.ForOrdinaryMembership(
                    context.TenantId,
                    membership,
                    exactScopeReference,
                    context.CorrelationId,
                    context.ActorId),
            TenantAuthorizationPath.SupportGrant when context.SupportGrant is { } supportGrant =>
                TenantContext.ForSupportGrant(
                    context.TenantId,
                    supportGrant,
                    exactScopeReference,
                    context.CorrelationId,
                    context.ActorId),
            _ => throw new ArgumentException("The test authority context has no supported authorization path.", nameof(context))
        };
        var scope = TenantWorkScope.IssueFromVerifiedAuthority(executionContext, scopeRequest);
        return new VerifiedDurableWorkReconciliationAuthorization(
            executionContext,
            scope,
            executionContext.ActorId!.Value,
            Guid.NewGuid(),
            executionContext.CorrelationId!.Value);
    }

    private static TenantContext ExactContext(
        TenantContext currentTenantContext,
        DurableWorkItem workItem)
    {
        var scopeValue = workItem.Scope.WarehouseId is { } warehouseId
            ? $"Warehouse:{warehouseId}"
            : workItem.Scope.BranchId is { } branchId
                ? $"Branch:{branchId}"
                : workItem.Scope.CompanyId is { } companyId
                    ? $"Company:{companyId}"
                    : $"Tenant:{workItem.TenantId.Value}";
        var scope = new ScopeReference(scopeValue);
        return currentTenantContext.AuthorizationPath switch
        {
            TenantAuthorizationPath.OrdinaryMembership when currentTenantContext.Membership is { } membership =>
                TenantContext.ForOrdinaryMembership(
                    currentTenantContext.TenantId,
                    membership,
                    scope,
                    workItem.Identity.CorrelationId,
                    currentTenantContext.ActorId),
            TenantAuthorizationPath.SupportGrant when currentTenantContext.SupportGrant is { } supportGrant =>
                TenantContext.ForSupportGrant(
                    currentTenantContext.TenantId,
                    supportGrant,
                    scope,
                    workItem.Identity.CorrelationId,
                    currentTenantContext.ActorId),
            _ => throw new ArgumentException("The test authority context has no supported authorization path.", nameof(currentTenantContext))
        };
    }
}
