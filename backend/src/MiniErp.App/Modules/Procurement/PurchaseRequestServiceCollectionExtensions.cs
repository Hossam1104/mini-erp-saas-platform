#pragma warning disable CS1591

using Microsoft.Extensions.DependencyInjection;

namespace MiniErp.App.Modules.Procurement;

public static class PurchaseRequestServiceCollectionExtensions
{
    public static IServiceCollection AddPurchaseRequestApprovalFoundation(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ProcurementTenantContextResolver>();
        services.AddSingleton<PurchaseRequestAuthorizationService>();
        services.AddSingleton<IPurchaseRequestPersistence, UnavailablePurchaseRequestPersistence>();
        services.AddSingleton<ISupplierQuotationPersistence, UnavailableSupplierQuotationPersistence>();
        services.AddSingleton<IPurchaseOrderPersistence, UnavailablePurchaseOrderPersistence>();
        services.AddSingleton<IGoodsReceiptPersistence, UnavailableGoodsReceiptPersistence>();
        services.AddSingleton<IPurchaseInvoiceHandoffPersistence, UnavailablePurchaseInvoiceHandoffPersistence>();
        services.AddSingleton<IPurchaseInvoiceMatchPersistence, UnavailablePurchaseInvoiceMatchPersistence>();
        services.AddSingleton<IPurchaseRequestApprovalPolicyProvider, DefaultPurchaseRequestApprovalPolicyProvider>();
        services.AddSingleton<IPurchaseRequestApprovalDelegationProvider, NoPurchaseRequestApprovalDelegationProvider>();
        services.AddSingleton<IPurchaseInvoiceMatchingTolerancePolicyProvider, ExactSafePurchaseInvoiceMatchingTolerancePolicyProvider>();
        services.AddSingleton<IPurchaseInvoiceMatchingResolutionPolicyProvider, DefaultPurchaseInvoiceMatchingResolutionPolicyProvider>();
        services.AddSingleton<IProcurementOrganizationScopeProvider, NoProcurementOrganizationScopeProvider>();
        services.AddSingleton<IProcurementWarehouseProvider, NoProcurementWarehouseProvider>();
        services.AddSingleton<PurchaseRequestService>();
        services.AddSingleton<SupplierQuotationService>();
        services.AddSingleton<PurchaseOrderService>();
        services.AddSingleton<GoodsReceiptService>();
        services.AddSingleton<PurchaseInvoiceHandoffService>();
        services.AddSingleton<PurchaseInvoiceMatchService>();
        return services;
    }
}

#pragma warning restore CS1591
