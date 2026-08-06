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
