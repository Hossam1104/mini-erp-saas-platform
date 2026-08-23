#pragma warning disable CS1591

using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.Modules.Audit;
using MiniErp.App.Modules.Inventory;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Inventory;

namespace MiniErp.Api;

public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        MapGet(endpoints, "/api/v1/inventory/warehouses", "inventory.warehouse.list", (InventoryRequestContext context, HttpContext http) =>
            new InventoryServiceAccessor(context, http).Service.ListWarehousesAsync(context, ParseGuid(http, "companyId"), ParseGuid(http, "branchId"), http.RequestAborted));
        MapGet(endpoints, "/api/v1/inventory/ledger", "inventory.ledger.list", (InventoryRequestContext context, HttpContext http) =>
            new InventoryServiceAccessor(context, http).Service.ListMovementsAsync(context, ParseGuid(http, "warehouseId"), ParseGuid(http, "companyId"), ParseGuid(http, "branchId"), ParseGuid(http, "productId"), http.RequestAborted));
        MapGet<InventoryMovementRecord>(endpoints, "/api/v1/inventory/ledger/{movementId:guid}", "inventory.ledger.read", (InventoryRequestContext context, HttpContext http) =>
            new InventoryServiceAccessor(context, http).Service.FindMovementAsync(context, ParseRouteGuid(http, "movementId"), http.RequestAborted));
        MapGet<InventoryAvailabilityRecord>(endpoints, "/api/v1/inventory/availability", "inventory.availability.read", (InventoryRequestContext context, HttpContext http) =>
        {
            return new InventoryServiceAccessor(context, http).Service.GetAvailabilityAsync(context, ParseGuid(http, "warehouseId") ?? Guid.Empty, ParseGuid(http, "companyId") ?? Guid.Empty, ParseGuid(http, "branchId"), ParseGuid(http, "productId") ?? Guid.Empty, ParseGuid(http, "unitOfMeasureId") ?? Guid.Empty, http.Request.Query["trackingIdentity"].FirstOrDefault(), http.RequestAborted);
        });
        MapGet(endpoints, "/api/v1/inventory/opening-balances", "inventory.opening.list", (InventoryRequestContext context, HttpContext http) =>
            new InventoryServiceAccessor(context, http).Service.ListOpeningBalancesAsync(context, ParseGuid(http, "warehouseId"), ParseGuid(http, "companyId"), ParseGuid(http, "branchId"), http.RequestAborted));
        MapGet<InventoryOpeningBalanceRecord>(endpoints, "/api/v1/inventory/opening-balances/{openingBalanceId:guid}", "inventory.opening.read", (InventoryRequestContext context, HttpContext http) =>
            new InventoryServiceAccessor(context, http).Service.FindOpeningBalanceAsync(context, ParseRouteGuid(http, "openingBalanceId"), http.RequestAborted));
        MapGet<IReadOnlyList<InventoryOpeningBalanceHistoryRecord>>(endpoints, "/api/v1/inventory/opening-balances/{openingBalanceId:guid}/history", "inventory.opening.history.read", (InventoryRequestContext context, HttpContext http) =>
            new InventoryServiceAccessor(context, http).Service.ReadOpeningHistoryAsync(context, ParseRouteGuid(http, "openingBalanceId"), http.RequestAborted));
        MapGet<IReadOnlyList<InventoryAuditRecord>>(endpoints, "/api/v1/inventory/opening-balances/{openingBalanceId:guid}/audit", "inventory.opening.audit.read", (InventoryRequestContext context, HttpContext http) =>
            new InventoryServiceAccessor(context, http).Service.ReadAuditAsync(context, "opening-balance", ParseRouteGuid(http, "openingBalanceId"), http.RequestAborted));
        MapGet(endpoints, "/api/v1/inventory/reservations", "inventory.reservation.list", (InventoryRequestContext context, HttpContext http) =>
            new InventoryServiceAccessor(context, http).Service.ListReservationsAsync(context, ParseGuid(http, "warehouseId"), ParseGuid(http, "companyId"), ParseGuid(http, "branchId"), ParseGuid(http, "productId"), http.RequestAborted));
        MapGet<InventoryReservationRecord>(endpoints, "/api/v1/inventory/reservations/{reservationId:guid}", "inventory.reservation.read", (InventoryRequestContext context, HttpContext http) =>
            new InventoryServiceAccessor(context, http).Service.FindReservationAsync(context, ParseRouteGuid(http, "reservationId"), http.RequestAborted));
        MapGet<IReadOnlyList<InventoryReservationHistoryRecord>>(endpoints, "/api/v1/inventory/reservations/{reservationId:guid}/history", "inventory.reservation.history.read", (InventoryRequestContext context, HttpContext http) =>
            new InventoryServiceAccessor(context, http).Service.ReadReservationHistoryAsync(context, ParseRouteGuid(http, "reservationId"), http.RequestAborted));
        MapGet<IReadOnlyList<InventoryAuditRecord>>(endpoints, "/api/v1/inventory/reservations/{reservationId:guid}/audit", "inventory.reservation.audit.read", (InventoryRequestContext context, HttpContext http) =>
            new InventoryServiceAccessor(context, http).Service.ReadAuditAsync(context, "reservation", ParseRouteGuid(http, "reservationId"), http.RequestAborted));
        MapGet(endpoints, "/api/v1/inventory/transfers", "inventory.transfer.list", (InventoryRequestContext context, HttpContext http) =>
            new InventoryServiceAccessor(context, http).Service.ListTransfersAsync(context, ParseGuid(http, "companyId"), ParseGuid(http, "branchId"), http.RequestAborted));
        MapGet<InventoryTransferRecord>(endpoints, "/api/v1/inventory/transfers/{transferId:guid}", "inventory.transfer.read", (InventoryRequestContext context, HttpContext http) =>
            new InventoryServiceAccessor(context, http).Service.FindTransferAsync(context, ParseRouteGuid(http, "transferId"), http.RequestAborted));
        MapGet<IReadOnlyList<InventoryTransferEventRecord>>(endpoints, "/api/v1/inventory/transfers/{transferId:guid}/history", "inventory.transfer.history.read", (InventoryRequestContext context, HttpContext http) =>
            new InventoryServiceAccessor(context, http).Service.ReadTransferHistoryAsync(context, ParseRouteGuid(http, "transferId"), http.RequestAborted));
        MapGet<IReadOnlyList<InventoryAuditRecord>>(endpoints, "/api/v1/inventory/transfers/{transferId:guid}/audit", "inventory.transfer.audit.read", (InventoryRequestContext context, HttpContext http) =>
            new InventoryServiceAccessor(context, http).Service.ReadAuditAsync(context, "transfer", ParseRouteGuid(http, "transferId"), http.RequestAborted));
        MapGet<InventoryCustomerReturnBoundaryRecord>(endpoints, "/api/v1/inventory/customer-returns/boundary", "inventory.customer-return.boundary.read", (InventoryRequestContext context, HttpContext http) =>
            Task.FromResult(new InventoryOperationResult<InventoryCustomerReturnBoundaryRecord>(true, "succeeded", new InventoryCustomerReturnBoundaryRecord(false, "unavailable", "sales_customer_return_handoff_required", "Sales integration boundary"))));

        endpoints.MapPost("/api/v1/inventory/opening-balances", async (InventoryOpeningBalanceCreateRequest? request, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(http, resolver, tenantResolver, audit, idem, "inventory.opening.create", request, (context, key) => request is null ? Task.FromResult(InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("validation_failed")) : service.CreateOpeningBalanceAsync(context, request, key, http.RequestAborted), setEtag: true))
            .WithName("inventory.opening.create").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.opening.create")));
        endpoints.MapPost("/api/v1/inventory/opening-balances/{openingBalanceId:guid}/validate", async (Guid openingBalanceId, InventoryOpeningBalanceActionRequest? body, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(openingBalanceId, body, http, resolver, tenantResolver, audit, idem, "inventory.opening.validate", (id, context, action, key, inventory) => inventory.ValidateOpeningBalanceAsync(context, id, ReadVersion(http)!, action?.Reason, key, http.RequestAborted)))
            .WithName("inventory.opening.validate").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.opening.validate")));
        endpoints.MapPost("/api/v1/inventory/opening-balances/{openingBalanceId:guid}/post", async (Guid openingBalanceId, InventoryOpeningBalanceActionRequest? body, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(openingBalanceId, body, http, resolver, tenantResolver, audit, idem, "inventory.opening.post", (id, context, action, key, inventory) => inventory.PostOpeningBalanceAsync(context, id, ReadVersion(http)!, action?.Reason, key, http.RequestAborted)))
            .WithName("inventory.opening.post").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.opening.post")));
        endpoints.MapPost("/api/v1/inventory/opening-balances/{openingBalanceId:guid}/correct", async (Guid openingBalanceId, InventoryOpeningBalanceActionRequest? body, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(openingBalanceId, body, http, resolver, tenantResolver, audit, idem, "inventory.opening.correct", (id, context, action, key, inventory) => inventory.CorrectOpeningBalanceAsync(context, id, ReadVersion(http)!, action?.Reason, key, http.RequestAborted)))
            .WithName("inventory.opening.correct").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.opening.correct")));
        endpoints.MapPost("/api/v1/inventory/reservations", async (InventoryReservationCreateRequest? request, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(http, resolver, tenantResolver, audit, idem, "inventory.reservation.create", request, (context, key) => request is null ? Task.FromResult(InventoryOperationResult<InventoryReservationRecord>.Failure("validation_failed")) : service.CreateReservationAsync(context, request, key, http.RequestAborted), setEtag: true))
            .WithName("inventory.reservation.create").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.reservation.create")));
        endpoints.MapPost("/api/v1/inventory/reservations/{reservationId:guid}/reduce", async (Guid reservationId, InventoryReservationActionRequest? body, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(reservationId, body, http, resolver, tenantResolver, audit, idem, "inventory.reservation.reduce", (id, context, action, key, inventory) => inventory.ReduceReservationAsync(context, id, ReadVersion(http)!, action?.Quantity ?? 0, action?.Reason, key, http.RequestAborted)))
            .WithName("inventory.reservation.reduce").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.reservation.reduce")));
        endpoints.MapPost("/api/v1/inventory/reservations/{reservationId:guid}/release", async (Guid reservationId, InventoryReservationActionRequest? body, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(reservationId, body, http, resolver, tenantResolver, audit, idem, "inventory.reservation.release", (id, context, action, key, inventory) => inventory.ReleaseReservationAsync(context, id, ReadVersion(http)!, action?.Reason, key, http.RequestAborted)))
            .WithName("inventory.reservation.release").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.reservation.release")));
        endpoints.MapPost("/api/v1/inventory/goods-receipts/{goodsReceiptId:guid}/lines/{goodsReceiptLineId:guid}/post", async (Guid goodsReceiptId, Guid goodsReceiptLineId, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(http, resolver, tenantResolver, audit, idem, "inventory.goods-receipt.post", new InventoryGoodsReceiptPostRequest(goodsReceiptId, goodsReceiptLineId, ReadVersion(http)), (context, key) => service.PostGoodsReceiptAsync(context, new InventoryGoodsReceiptPostRequest(goodsReceiptId, goodsReceiptLineId, ReadVersion(http)), key, http.RequestAborted), setEtag: false))
            .WithName("inventory.goods-receipt.post").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.goods-receipt.post")));
        endpoints.MapPost("/api/v1/inventory/supplier-returns/{supplierReturnId:guid}/post", async (Guid supplierReturnId, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(http, resolver, tenantResolver, audit, idem, "inventory.supplier-return.post", new InventorySupplierReturnPostRequest(supplierReturnId, ReadVersion(http)), (context, key) => service.PostSupplierReturnAsync(context, new InventorySupplierReturnPostRequest(supplierReturnId, ReadVersion(http)), key, http.RequestAborted), setEtag: false))
            .WithName("inventory.supplier-return.post").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.supplier-return.post")));
        endpoints.MapPost("/api/v1/inventory/transfers", async (InventoryTransferCreateRequest? request, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(http, resolver, tenantResolver, audit, idem, "inventory.transfer.create", request, (context, key) => request is null ? Task.FromResult(InventoryOperationResult<InventoryTransferRecord>.Failure("validation_failed")) : service.CreateTransferAsync(context, request, key, http.RequestAborted), setEtag: true))
            .WithName("inventory.transfer.create").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.transfer.create")));
        endpoints.MapPost("/api/v1/inventory/transfers/{transferId:guid}/complete-direct", async (Guid transferId, InventoryTransferActionRequest? body, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(transferId, body, http, resolver, tenantResolver, audit, idem, "inventory.transfer.direct", (id, context, action, key, inventory) => inventory.PostDirectTransferAsync(context, id, ReadVersion(http)!, action, key, http.RequestAborted)))
            .WithName("inventory.transfer.direct").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.transfer.direct")));
        endpoints.MapPost("/api/v1/inventory/transfers/{transferId:guid}/ship", async (Guid transferId, InventoryTransferActionRequest? body, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(transferId, body, http, resolver, tenantResolver, audit, idem, "inventory.transfer.ship", (id, context, action, key, inventory) => inventory.ShipTransferAsync(context, id, ReadVersion(http)!, action, key, http.RequestAborted)))
            .WithName("inventory.transfer.ship").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.transfer.ship")));
        endpoints.MapPost("/api/v1/inventory/transfers/{transferId:guid}/receive", async (Guid transferId, InventoryTransferActionRequest? body, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(transferId, body, http, resolver, tenantResolver, audit, idem, "inventory.transfer.receive", (id, context, action, key, inventory) => inventory.ReceiveTransferAsync(context, id, ReadVersion(http)!, action, key, http.RequestAborted)))
            .WithName("inventory.transfer.receive").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.transfer.receive")));
        endpoints.MapPost("/api/v1/inventory/transfers/{transferId:guid}/resolve-shortage", async (Guid transferId, InventoryTransferActionRequest? body, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(transferId, body, http, resolver, tenantResolver, audit, idem, "inventory.transfer.shortage-resolve", (id, context, action, key, inventory) => inventory.ResolveTransferShortageAsync(context, id, ReadVersion(http)!, action, key, http.RequestAborted)))
            .WithName("inventory.transfer.shortage-resolve").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.transfer.shortage-resolve")));
        endpoints.MapPost("/api/v1/inventory/transfers/{transferId:guid}/cancel", async (Guid transferId, InventoryTransferActionRequest? body, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(transferId, body, http, resolver, tenantResolver, audit, idem, "inventory.transfer.cancel", (id, context, action, key, inventory) => inventory.CancelTransferAsync(context, id, ReadVersion(http)!, action, key, http.RequestAborted)))
            .WithName("inventory.transfer.cancel").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.transfer.cancel")));

        MapGet(endpoints, "/api/v1/inventory/reason-codes", "inventory.reason.list", (InventoryRequestContext context, HttpContext http) =>
            new InventoryServiceAccessor(context, http).Service.ListReasonCodesAsync(context, ParseEnum<InventoryReasonCategory>(http, "category"), ParseBool(http, "includeInactive"), ParseGuid(http, "warehouseId"), ParseGuid(http, "companyId"), ParseGuid(http, "branchId"), http.RequestAborted));
        MapGet<InventoryReasonCodeRecord>(endpoints, "/api/v1/inventory/reason-codes/{reasonCodeId:guid}", "inventory.reason.read", (InventoryRequestContext context, HttpContext http) =>
            new InventoryServiceAccessor(context, http).Service.FindReasonCodeAsync(context, ParseRouteGuid(http, "reasonCodeId"), http.RequestAborted));
        MapGet(endpoints, "/api/v1/inventory/adjustments", "inventory.adjustment.list", (InventoryRequestContext context, HttpContext http) =>
            new InventoryServiceAccessor(context, http).Service.ListAdjustmentsAsync(context, ParseGuid(http, "warehouseId"), ParseGuid(http, "companyId"), ParseGuid(http, "branchId"), http.RequestAborted));
        MapGet<InventoryAdjustmentRecord>(endpoints, "/api/v1/inventory/adjustments/{adjustmentId:guid}", "inventory.adjustment.read", (InventoryRequestContext context, HttpContext http) =>
            new InventoryServiceAccessor(context, http).Service.FindAdjustmentAsync(context, ParseRouteGuid(http, "adjustmentId"), http.RequestAborted));
        MapGet<IReadOnlyList<InventoryControlHistoryRecord>>(endpoints, "/api/v1/inventory/adjustments/{adjustmentId:guid}/history", "inventory.adjustment.history.read", (InventoryRequestContext context, HttpContext http) =>
            new InventoryServiceAccessor(context, http).Service.ReadControlHistoryAsync(context, "adjustment", ParseRouteGuid(http, "adjustmentId"), http.RequestAborted));
        MapGet<IReadOnlyList<InventoryAuditRecord>>(endpoints, "/api/v1/inventory/adjustments/{adjustmentId:guid}/audit", "inventory.adjustment.audit.read", (InventoryRequestContext context, HttpContext http) =>
            new InventoryServiceAccessor(context, http).Service.ReadAuditAsync(context, "adjustment", ParseRouteGuid(http, "adjustmentId"), http.RequestAborted));
        MapGet(endpoints, "/api/v1/inventory/counts", "inventory.count.list", (InventoryRequestContext context, HttpContext http) =>
            new InventoryServiceAccessor(context, http).Service.ListCountsAsync(context, ParseGuid(http, "warehouseId"), ParseGuid(http, "companyId"), ParseGuid(http, "branchId"), http.RequestAborted));
        MapGet<InventoryCountRecord>(endpoints, "/api/v1/inventory/counts/{countId:guid}", "inventory.count.read", (InventoryRequestContext context, HttpContext http) =>
            new InventoryServiceAccessor(context, http).Service.FindCountAsync(context, ParseRouteGuid(http, "countId"), false, http.RequestAborted));
        MapGet<InventoryCountRecord>(endpoints, "/api/v1/inventory/counts/{countId:guid}/counter-view", "inventory.count.counter.read", (InventoryRequestContext context, HttpContext http) =>
            new InventoryServiceAccessor(context, http).Service.FindCountAsync(context, ParseRouteGuid(http, "countId"), true, http.RequestAborted));
        MapGet<IReadOnlyList<InventoryControlHistoryRecord>>(endpoints, "/api/v1/inventory/counts/{countId:guid}/history", "inventory.count.history.read", (InventoryRequestContext context, HttpContext http) =>
            new InventoryServiceAccessor(context, http).Service.ReadControlHistoryAsync(context, "count", ParseRouteGuid(http, "countId"), http.RequestAborted));
        MapGet<IReadOnlyList<InventoryAuditRecord>>(endpoints, "/api/v1/inventory/counts/{countId:guid}/audit", "inventory.count.audit.read", (InventoryRequestContext context, HttpContext http) =>
            new InventoryServiceAccessor(context, http).Service.ReadAuditAsync(context, "count", ParseRouteGuid(http, "countId"), http.RequestAborted));
        MapGet(endpoints, "/api/v1/inventory/stock-issues", "inventory.issue.list", (InventoryRequestContext context, HttpContext http) =>
            new InventoryServiceAccessor(context, http).Service.ListStockIssuesAsync(context, ParseGuid(http, "warehouseId"), ParseGuid(http, "companyId"), ParseGuid(http, "branchId"), http.RequestAborted));
        MapGet<InventoryStockIssueRecord>(endpoints, "/api/v1/inventory/stock-issues/{stockIssueId:guid}", "inventory.issue.read", (InventoryRequestContext context, HttpContext http) =>
            new InventoryServiceAccessor(context, http).Service.FindStockIssueAsync(context, ParseRouteGuid(http, "stockIssueId"), http.RequestAborted));
        MapGet<IReadOnlyList<InventoryControlHistoryRecord>>(endpoints, "/api/v1/inventory/stock-issues/{stockIssueId:guid}/history", "inventory.issue.history.read", (InventoryRequestContext context, HttpContext http) =>
            new InventoryServiceAccessor(context, http).Service.ReadControlHistoryAsync(context, "stock-issue", ParseRouteGuid(http, "stockIssueId"), http.RequestAborted));
        MapGet<IReadOnlyList<InventoryAuditRecord>>(endpoints, "/api/v1/inventory/stock-issues/{stockIssueId:guid}/audit", "inventory.issue.audit.read", (InventoryRequestContext context, HttpContext http) =>
            new InventoryServiceAccessor(context, http).Service.ReadAuditAsync(context, "stock-issue", ParseRouteGuid(http, "stockIssueId"), http.RequestAborted));

        endpoints.MapPost("/api/v1/inventory/reason-codes", async (InventoryReasonCodeCreateRequest? request, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(http, resolver, tenantResolver, audit, idem, "inventory.reason.create", request, (context, key) => request is null ? Task.FromResult(InventoryOperationResult<InventoryReasonCodeRecord>.Failure("validation_failed")) : service.CreateReasonCodeAsync(context, request, key, http.RequestAborted), setEtag: true))
            .WithName("inventory.reason.create").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.reason.create")));
        endpoints.MapPost("/api/v1/inventory/reason-codes/{reasonCodeId:guid}/update", async (Guid reasonCodeId, InventoryReasonCodeUpdateRequest? request, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(reasonCodeId, request, http, resolver, tenantResolver, audit, idem, "inventory.reason.update", (id, context, body, key, inventory) => body is null ? Task.FromResult(InventoryOperationResult<InventoryReasonCodeRecord>.Failure("validation_failed")) : inventory.UpdateReasonCodeAsync(context, id, ReadVersion(http)!, body, key, http.RequestAborted)))
            .WithName("inventory.reason.update").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.reason.update")));
        endpoints.MapPost("/api/v1/inventory/adjustments", async (InventoryAdjustmentCreateRequest? request, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(http, resolver, tenantResolver, audit, idem, "inventory.adjustment.create", request, (context, key) => request is null ? Task.FromResult(InventoryOperationResult<InventoryAdjustmentRecord>.Failure("validation_failed")) : service.CreateAdjustmentAsync(context, request, key, http.RequestAborted), setEtag: true))
            .WithName("inventory.adjustment.create").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.adjustment.create")));
        endpoints.MapPost("/api/v1/inventory/adjustments/{adjustmentId:guid}/submit", async (Guid id, InventoryControlActionRequest? body, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(id, body, http, resolver, tenantResolver, audit, idem, "inventory.adjustment.submit", (value, context, action, key, inventory) => inventory.SubmitAdjustmentAsync(context, value, ReadVersion(http)!, action?.Reason, key, http.RequestAborted)))
            .WithName("inventory.adjustment.submit").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.adjustment.submit")));
        endpoints.MapPost("/api/v1/inventory/adjustments/{adjustmentId:guid}/approve", async (Guid id, InventoryControlActionRequest? body, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(id, body, http, resolver, tenantResolver, audit, idem, "inventory.adjustment.approve", (value, context, action, key, inventory) => inventory.ApproveAdjustmentAsync(context, value, ReadVersion(http)!, action?.Reason, key, http.RequestAborted)))
            .WithName("inventory.adjustment.approve").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.adjustment.approve")));
        endpoints.MapPost("/api/v1/inventory/adjustments/{adjustmentId:guid}/reject", async (Guid id, InventoryControlActionRequest? body, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(id, body, http, resolver, tenantResolver, audit, idem, "inventory.adjustment.reject", (value, context, action, key, inventory) => inventory.RejectAdjustmentAsync(context, value, ReadVersion(http)!, action?.Reason, false, key, http.RequestAborted)))
            .WithName("inventory.adjustment.reject").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.adjustment.reject")));
        endpoints.MapPost("/api/v1/inventory/adjustments/{adjustmentId:guid}/return", async (Guid id, InventoryControlActionRequest? body, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(id, body, http, resolver, tenantResolver, audit, idem, "inventory.adjustment.return", (value, context, action, key, inventory) => inventory.RejectAdjustmentAsync(context, value, ReadVersion(http)!, action?.Reason, true, key, http.RequestAborted)))
            .WithName("inventory.adjustment.return").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.adjustment.return")));
        endpoints.MapPost("/api/v1/inventory/adjustments/{adjustmentId:guid}/post", async (Guid id, InventoryControlActionRequest? body, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(id, body, http, resolver, tenantResolver, audit, idem, "inventory.adjustment.post", (value, context, action, key, inventory) => inventory.PostAdjustmentAsync(context, value, ReadVersion(http)!, action?.Reason, key, http.RequestAborted)))
            .WithName("inventory.adjustment.post").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.adjustment.post")));

        endpoints.MapPost("/api/v1/inventory/counts", async (InventoryCountCreateRequest? request, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(http, resolver, tenantResolver, audit, idem, "inventory.count.create", request, (context, key) => request is null ? Task.FromResult(InventoryOperationResult<InventoryCountRecord>.Failure("validation_failed")) : service.CreateCountAsync(context, request, key, http.RequestAborted), setEtag: true))
            .WithName("inventory.count.create").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.count.create")));
        endpoints.MapPost("/api/v1/inventory/counts/{countId:guid}/submit", async (Guid id, InventoryCountSubmitRequest? body, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(id, body, http, resolver, tenantResolver, audit, idem, "inventory.count.submit", (value, context, action, key, inventory) => action is null ? Task.FromResult(InventoryOperationResult<InventoryCountRecord>.Failure("validation_failed")) : inventory.SubmitCountAsync(context, value, ReadVersion(http)!, action, key, http.RequestAborted)))
            .WithName("inventory.count.submit").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.count.submit")));
        endpoints.MapPost("/api/v1/inventory/counts/{countId:guid}/approve", async (Guid id, InventoryCountActionRequest? body, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(id, body, http, resolver, tenantResolver, audit, idem, "inventory.count.approve", (value, context, action, key, inventory) => inventory.ApproveCountAsync(context, value, ReadVersion(http)!, action?.Reason, key, http.RequestAborted)))
            .WithName("inventory.count.approve").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.count.approve")));
        endpoints.MapPost("/api/v1/inventory/counts/{countId:guid}/variance-reason", async (Guid id, InventoryCountVarianceReasonRequest? body, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(id, body, http, resolver, tenantResolver, audit, idem, "inventory.count.variance-reason", (value, context, action, key, inventory) => action is null ? Task.FromResult(InventoryOperationResult<InventoryCountRecord>.Failure("validation_failed")) : inventory.RecordCountVarianceReasonAsync(context, value, ReadVersion(http)!, action, key, http.RequestAborted)))
            .WithName("inventory.count.variance-reason").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.count.variance-reason")));
        endpoints.MapPost("/api/v1/inventory/counts/{countId:guid}/reject", async (Guid id, InventoryCountActionRequest? body, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(id, body, http, resolver, tenantResolver, audit, idem, "inventory.count.reject", (value, context, action, key, inventory) => inventory.RejectCountAsync(context, value, ReadVersion(http)!, action?.Reason, false, key, http.RequestAborted)))
            .WithName("inventory.count.reject").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.count.reject")));
        endpoints.MapPost("/api/v1/inventory/counts/{countId:guid}/return", async (Guid id, InventoryCountActionRequest? body, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(id, body, http, resolver, tenantResolver, audit, idem, "inventory.count.return", (value, context, action, key, inventory) => inventory.RejectCountAsync(context, value, ReadVersion(http)!, action?.Reason, true, key, http.RequestAborted)))
            .WithName("inventory.count.return").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.count.return")));
        endpoints.MapPost("/api/v1/inventory/counts/{countId:guid}/recount", async (Guid id, InventoryCountActionRequest? body, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(id, body, http, resolver, tenantResolver, audit, idem, "inventory.count.recount", (value, context, action, key, inventory) => inventory.RequestCountRecountAsync(context, value, ReadVersion(http)!, action?.Reason, key, http.RequestAborted)))
            .WithName("inventory.count.recount").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.count.recount")));
        endpoints.MapPost("/api/v1/inventory/counts/{countId:guid}/resnapshot", async (Guid id, InventoryCountActionRequest? body, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(id, body, http, resolver, tenantResolver, audit, idem, "inventory.count.resnapshot", (value, context, action, key, inventory) => inventory.ResnapshotCountAsync(context, value, ReadVersion(http)!, action?.Reason, key, http.RequestAborted)))
            .WithName("inventory.count.resnapshot").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.count.resnapshot")));
        endpoints.MapPost("/api/v1/inventory/counts/{countId:guid}/post", async (Guid id, InventoryCountActionRequest? body, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(id, body, http, resolver, tenantResolver, audit, idem, "inventory.count.post", (value, context, action, key, inventory) => inventory.PostCountAsync(context, value, ReadVersion(http)!, action?.Reason, key, http.RequestAborted)))
            .WithName("inventory.count.post").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.count.post")));

        endpoints.MapPost("/api/v1/inventory/stock-issues", async (InventoryStockIssueCreateRequest? request, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(http, resolver, tenantResolver, audit, idem, "inventory.issue.create", request, (context, key) => request is null ? Task.FromResult(InventoryOperationResult<InventoryStockIssueRecord>.Failure("validation_failed")) : service.CreateStockIssueAsync(context, request, key, http.RequestAborted), setEtag: true))
            .WithName("inventory.issue.create").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.issue.create")));
        endpoints.MapPost("/api/v1/inventory/stock-issues/{stockIssueId:guid}/submit", async (Guid id, InventoryControlActionRequest? body, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(id, body, http, resolver, tenantResolver, audit, idem, "inventory.issue.submit", (value, context, action, key, inventory) => inventory.SubmitStockIssueAsync(context, value, ReadVersion(http)!, action?.Reason, key, http.RequestAborted)))
            .WithName("inventory.issue.submit").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.issue.submit")));
        endpoints.MapPost("/api/v1/inventory/stock-issues/{stockIssueId:guid}/approve", async (Guid id, InventoryControlActionRequest? body, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(id, body, http, resolver, tenantResolver, audit, idem, "inventory.issue.approve", (value, context, action, key, inventory) => inventory.ApproveStockIssueAsync(context, value, ReadVersion(http)!, action?.Reason, key, http.RequestAborted)))
            .WithName("inventory.issue.approve").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.issue.approve")));
        endpoints.MapPost("/api/v1/inventory/stock-issues/{stockIssueId:guid}/reject", async (Guid id, InventoryControlActionRequest? body, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(id, body, http, resolver, tenantResolver, audit, idem, "inventory.issue.reject", (value, context, action, key, inventory) => inventory.RejectStockIssueAsync(context, value, ReadVersion(http)!, action?.Reason, false, key, http.RequestAborted)))
            .WithName("inventory.issue.reject").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.issue.reject")));
        endpoints.MapPost("/api/v1/inventory/stock-issues/{stockIssueId:guid}/return", async (Guid id, InventoryControlActionRequest? body, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(id, body, http, resolver, tenantResolver, audit, idem, "inventory.issue.return", (value, context, action, key, inventory) => inventory.RejectStockIssueAsync(context, value, ReadVersion(http)!, action?.Reason, true, key, http.RequestAborted)))
            .WithName("inventory.issue.return").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.issue.return")));
        endpoints.MapPost("/api/v1/inventory/stock-issues/{stockIssueId:guid}/post", async (Guid id, InventoryControlActionRequest? body, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(id, body, http, resolver, tenantResolver, audit, idem, "inventory.issue.post", (value, context, action, key, inventory) => inventory.PostStockIssueAsync(context, value, ReadVersion(http)!, action?.Reason, key, http.RequestAborted)))
            .WithName("inventory.issue.post").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.issue.post")));
        endpoints.MapPost("/api/v1/inventory/ledger/{movementId:guid}/correct", async (Guid id, InventoryCorrectionRequest? body, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, InventoryService service) =>
            await ExecuteMutationAsync(id, body, http, resolver, tenantResolver, audit, idem, "inventory.movement.correct", (value, context, action, key, inventory) => action is null ? Task.FromResult(InventoryOperationResult<InventoryMovementRecord>.Failure("validation_failed")) : inventory.CorrectMovementAsync(context, value, ReadVersion(http)!, action, key, http.RequestAborted)))
            .WithName("inventory.movement.correct").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.movement.correct")));

        MapValuationGet<IReadOnlyList<InventoryValuationPolicyRecord>>(endpoints, "/api/v1/inventory/valuation/policies", "inventory.valuation.policy.read", (context, http, valuation) => valuation.ListPoliciesAsync(context, ParseGuid(http, "companyId") ?? Guid.Empty, http.RequestAborted));
        MapValuationGet<IReadOnlyList<InventoryValuationStateRecord>>(endpoints, "/api/v1/inventory/valuation/states", "inventory.valuation.state.read", (context, http, valuation) => valuation.ListStatesAsync(context, ParseValuationQuery(http), http.RequestAborted));
        MapValuationGet<IReadOnlyList<InventoryMovementValuationEventRecord>>(endpoints, "/api/v1/inventory/valuation/history", "inventory.valuation.history.read", (context, http, valuation) => valuation.ListEventsAsync(context, ParseValuationQuery(http), http.RequestAborted));
        MapValuationGet<IReadOnlyList<InventoryValuationReconciliationRecord>>(endpoints, "/api/v1/inventory/valuation/reconciliation", "inventory.valuation.reconciliation.read", (context, http, valuation) => valuation.ReconcileAsync(context, ParseValuationQuery(http), http.RequestAborted));
        MapValuationGet<IReadOnlyList<InventoryFinanceValuationHandoffRecord>>(endpoints, "/api/v1/inventory/valuation/finance-handoffs", "inventory.valuation.finance-handoff.read", (context, http, valuation) => valuation.ListFinanceHandoffsAsync(context, ParseValuationQuery(http), http.RequestAborted));
        MapValuationGet<InventoryValuationSummaryRecord>(endpoints, "/api/v1/inventory/valuation/summary", "inventory.valuation.summary.read", (context, http, valuation) => valuation.SummaryAsync(context, ParseValuationQuery(http), http.RequestAborted));
        MapValuationGet<IReadOnlyList<InventoryMovementValuationEventRecord>>(endpoints, "/api/v1/inventory/valuation/pending", "inventory.valuation.pending.read", (context, http, valuation) => valuation.ListEventsAsync(context, ParseValuationQuery(http) with { Status = InventoryValuationEventStatus.Pending }, http.RequestAborted));
        MapValuationGet<IReadOnlyList<InventoryValuationReconciliationRecord>>(endpoints, "/api/v1/inventory/valuation/in-transit", "inventory.valuation.in-transit.read", (context, http, valuation) => valuation.ReconcileAsync(context, ParseValuationQuery(http), http.RequestAborted));
        MapValuationExport(endpoints);
        endpoints.MapPost("/api/v1/inventory/valuation/policies", async (InventoryValuationPolicyRequest? request, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem) =>
            await ExecuteMutationAsync(http, resolver, tenantResolver, audit, idem, "inventory.valuation.policy.create", request, (context, key) => request is null ? Task.FromResult(InventoryOperationResult<InventoryValuationPolicyRecord>.Failure("validation_failed")) : http.RequestServices.GetRequiredService<InventoryValuationService>().CreatePolicyAsync(context, request, key, http.RequestAborted), setEtag: true))
            .WithName("inventory.valuation.policy.create").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.valuation.policy.create")));
        endpoints.MapPost("/api/v1/inventory/valuation/process", async (InventoryValuationProcessRequest? request, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem) =>
            await ExecuteMutationAsync(http, resolver, tenantResolver, audit, idem, "inventory.valuation.process", request, (context, key) => request is null ? Task.FromResult(InventoryOperationResult<InventoryValuationProcessResult>.Failure("validation_failed")) : http.RequestServices.GetRequiredService<InventoryValuationService>().ProcessAsync(context, new InventoryValuationProcessCommand(request.CompanyId, request.BranchId, request.WarehouseId, request.ProductId, request.UnitOfMeasureId, context.ActorId, DateTimeOffset.UtcNow, context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"), key, InventoryFingerprints.Create(request)), http.RequestAborted), setEtag: false))
            .WithName("inventory.valuation.process").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.valuation.process")));
        endpoints.MapPost("/api/v1/inventory/valuation/history/{valuationEventId:guid}/correction", async (Guid valuationEventId, InventoryValuationCorrectionRequest? request, HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem) =>
            await ExecuteMutationAsync(http, resolver, tenantResolver, audit, idem, "inventory.valuation.correction", request, (context, key) => request is null ? Task.FromResult(InventoryOperationResult<InventoryMovementValuationEventRecord>.Failure("validation_failed")) : http.RequestServices.GetRequiredService<InventoryValuationService>().CorrectAsync(context, new InventoryValuationCorrectionCommand(valuationEventId, request.AuthoritativeSourceRevisionId, request.Reason, context.ActorId, DateTimeOffset.UtcNow, context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"), key, InventoryFingerprints.Create(new { valuationEventId, request })), http.RequestAborted), setEtag: false))
            .WithName("inventory.valuation.correction").WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("inventory.valuation.correction")));

        return endpoints;
    }

    private static void MapGet<T>(IEndpointRouteBuilder endpoints, string route, string operationId, Func<InventoryRequestContext, HttpContext, Task<InventoryOperationResult<T>>> operation)
    {
        endpoints.MapGet(route, async (HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver) =>
        {
            var foundation = await resolver.ResolveAsync(http, http.RequestAborted); var resolution = tenantResolver.Resolve(foundation);
            if (!resolution.Allowed || resolution.Context is null) return await Problem(http, foundation.SecurityProfile == FoundationSecurityProfile.Anonymous ? 401 : 403, resolution.Code, operationId);
            var result = await operation(resolution.Context, http); return ToResult(http, result, operationId, setEtag: false);
        }).WithName(operationId).WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired(operationId)));
    }

    private static void MapValuationGet<T>(IEndpointRouteBuilder endpoints, string route, string operationId, Func<InventoryRequestContext, HttpContext, InventoryValuationService, Task<InventoryOperationResult<T>>> operation)
    {
        endpoints.MapGet(route, async (HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, InventoryValuationService valuation) =>
        {
            var foundation = await resolver.ResolveAsync(http, http.RequestAborted); var resolution = tenantResolver.Resolve(foundation);
            if (!resolution.Allowed || resolution.Context is null) return await Problem(http, foundation.SecurityProfile == FoundationSecurityProfile.Anonymous ? 401 : 403, resolution.Code, operationId);
            var result = await operation(resolution.Context, http, valuation); return ToResult(http, result, operationId, setEtag: false);
        }).WithName(operationId).WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired(operationId)));
    }

    private static void MapValuationExport(IEndpointRouteBuilder endpoints)
    {
        const string operationId = "inventory.valuation.export";
        endpoints.MapGet("/api/v1/inventory/valuation/export", async (HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, InventoryValuationService valuation) =>
        {
            var foundation = await resolver.ResolveAsync(http, http.RequestAborted);
            var resolution = tenantResolver.Resolve(foundation);
            if (!resolution.Allowed || resolution.Context is null)
                return await Problem(http, foundation.SecurityProfile == FoundationSecurityProfile.Anonymous ? 401 : 403, resolution.Code, operationId);

            var result = await valuation.ExportAsync(resolution.Context, ParseValuationQuery(http), http.RequestAborted);
            if (!result.Succeeded || result.Value is null)
                return ToResult(http, result, operationId, setEtag: false);

            var export = result.Value;
            return Results.File(Encoding.UTF8.GetBytes(export.Content), export.ContentType, export.FileName);
        }).WithName(operationId).WithTags("Inventory").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired(operationId)));
    }

    private static async Task<IResult> ExecuteMutationAsync<TBody, TValue>(
        Guid resourceId,
        TBody? body,
        HttpContext http,
        ITrustedRequestContextResolver resolver,
        InventoryTenantContextResolver tenantResolver,
        FoundationAuditCoordinator audit,
        LocalMasterDataIdempotencyStore idem,
        string operationId,
        Func<Guid, InventoryRequestContext, TBody?, string?, InventoryService, Task<InventoryOperationResult<TValue>>> operation)
    {
        var version = ReadVersion(http);
        if (version is null) return await Problem(http, 400, "validation_failed", operationId);
        var key = Key(http);
        if (!FoundationCorrelation.IsValid(key)) return await Problem(http, 400, "idempotency_key_invalid", operationId);
        if (!await Antiforgery(http)) return await Problem(http, 403, "antiforgery_failed", operationId);
        var foundation = await resolver.ResolveAsync(http, http.RequestAborted);
        var resolution = tenantResolver.Resolve(foundation);
        if (!resolution.Allowed || resolution.Context is null) return await Problem(http, 403, resolution.Code, operationId);
        var binding = new FoundationIdempotencyBinding(resolution.Context.ActorId, resolution.Context.TenantId.Value, FoundationOperationCatalog.GetRequired(operationId).SecurityProfile, operationId);
        var check = idem.Begin(key!, binding, JsonSerializer.Serialize(new { resourceId, version, body }), DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10));
        if (check.Decision == LocalMasterDataIdempotencyDecision.Replay && check.Response is TValue replay)
        {
            http.Response.Headers["X-Idempotent-Replay"] = "true";
            return ToResult(http, InventoryOperationResult<TValue>.Success(replay), operationId, setEtag: true);
        }
        if (check.Decision is not LocalMasterDataIdempotencyDecision.New) return await Problem(http, 409, "idempotency_conflict", operationId);
        var committed = false;
        try
        {
            var execution = await audit.ExecuteProtectedAsync(foundation, operationId, Correlation(http), FoundationAuditReason.Allowed, () => operation(resourceId, resolution.Context, body, key, http.RequestServices.GetRequiredService<InventoryService>()), idempotencyKey: key, operationVersion: Convert.ToBase64String(version), cancellationToken: http.RequestAborted);
            if (!execution.Succeeded || execution.Value is null) return await Problem(http, 503, execution.Code, operationId);
            var result = execution.Value;
            if (result.Succeeded && result.Value is not null) { idem.Commit(key!, binding, result.Value); committed = true; }
            return ToResult(http, result, operationId, setEtag: true);
        }
        finally
        {
            if (!committed) idem.Release(key!, binding);
        }
    }

    private static async Task<IResult> ExecuteMutationAsync<TRequest, TValue>(HttpContext http, ITrustedRequestContextResolver resolver, InventoryTenantContextResolver tenantResolver, FoundationAuditCoordinator audit, LocalMasterDataIdempotencyStore idem, string operationId, TRequest? request, Func<InventoryRequestContext, string?, Task<InventoryOperationResult<TValue>>> operation, bool setEtag)
    {
        if (!await Antiforgery(http)) return await Problem(http, 403, "antiforgery_failed", operationId);
        var key = Key(http); if (!FoundationCorrelation.IsValid(key)) return await Problem(http, 400, "idempotency_key_invalid", operationId);
        var foundation = await resolver.ResolveAsync(http, http.RequestAborted); var resolution = tenantResolver.Resolve(foundation);
        if (!resolution.Allowed || resolution.Context is null) return await Problem(http, 403, resolution.Code, operationId);
        var binding = new FoundationIdempotencyBinding(resolution.Context.ActorId, resolution.Context.TenantId.Value, FoundationOperationCatalog.GetRequired(operationId).SecurityProfile, operationId); var fingerprint = JsonSerializer.Serialize(request);
        var check = idem.Begin(key!, binding, fingerprint, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10));
        if (check.Decision == LocalMasterDataIdempotencyDecision.Replay && check.Response is TValue replay)
        {
            http.Response.Headers["X-Idempotent-Replay"] = "true";
            return ToResult(http, InventoryOperationResult<TValue>.Success(replay), operationId, setEtag);
        }
        if (check.Decision is not LocalMasterDataIdempotencyDecision.New) return await Problem(http, 409, "idempotency_conflict", operationId);
        var committed = false;
        try
        {
            var execution = await audit.ExecuteProtectedAsync(foundation, operationId, Correlation(http), FoundationAuditReason.Allowed, () => operation(resolution.Context, key), idempotencyKey: key, operationVersion: "inventory.v1", cancellationToken: http.RequestAborted);
            if (!execution.Succeeded || execution.Value is null) return await Problem(http, 503, execution.Code, operationId);
            var result = execution.Value;
            if (result.Succeeded && result.Value is not null) { idem.Commit(key!, binding, result.Value); committed = true; }
            return ToResult(http, result, operationId, setEtag);
        }
        finally
        {
            if (!committed) idem.Release(key!, binding);
        }
    }

    private sealed record InventoryServiceAccessor(InventoryRequestContext Context, HttpContext Http)
    {
        public InventoryService Service => Http.RequestServices.GetRequiredService<InventoryService>();
    }

    private static IResult ToResult<T>(HttpContext http, InventoryOperationResult<T> result, string operationId, bool setEtag)
    {
        if (result.Succeeded && result.Value is not null)
        {
            if (setEtag && result.Value is InventoryOpeningBalanceRecord opening) http.Response.Headers.ETag = $"\"{Convert.ToBase64String(opening.Version)}\"";
            if (setEtag && result.Value is InventoryReservationRecord reservation) http.Response.Headers.ETag = $"\"{Convert.ToBase64String(reservation.Version)}\"";
            if (setEtag && result.Value is InventoryTransferRecord transfer) http.Response.Headers.ETag = $"\"{Convert.ToBase64String(transfer.Version)}\"";
            if (setEtag && result.Value is InventoryReasonCodeRecord reason) http.Response.Headers.ETag = $"\"{Convert.ToBase64String(reason.Version)}\"";
            if (setEtag && result.Value is InventoryAdjustmentRecord adjustment) http.Response.Headers.ETag = $"\"{Convert.ToBase64String(adjustment.Version)}\"";
            if (setEtag && result.Value is InventoryCountRecord count) http.Response.Headers.ETag = $"\"{Convert.ToBase64String(count.Version)}\"";
            if (setEtag && result.Value is InventoryStockIssueRecord issue) http.Response.Headers.ETag = $"\"{Convert.ToBase64String(issue.Version)}\"";
            if (setEtag && result.Value is InventoryMovementRecord movement) http.Response.Headers.ETag = $"\"{Convert.ToBase64String(movement.Version)}\"";
            return Results.Json(result.Value);
        }
        var status = result.Code is "forbidden" or "tenant_context_failed" or "warehouse_not_available" ? 403 : result.Code is "not_found" ? 404 : result.Code is "persistence_unavailable" or "inventory_handoff_pending" ? 503 : result.Code is "conflict" or "duplicate_or_conflict" or "insufficient_available" or "idempotency_conflict" or "inventory_handoff_reconciliation_conflict" or "resnapshot_required" or "reservation_reconciliation_required" or "negative_stock_or_reservation" or "valuation_concurrency_conflict" or "valuation_policy_overlap" or "valuation_policy_transition_requires_rebaseline" ? 409 : 400;
        return Results.Problem(statusCode: status, title: status == 403 ? "Access denied" : "Inventory operation failed", detail: "The Inventory operation could not be completed.", type: $"https://api.minierp.local/problems/{result.Code}", extensions: new Dictionary<string, object?> { ["code"] = result.Code, ["correlationId"] = Correlation(http), ["operationId"] = operationId });
    }

    private static Guid? ParseGuid(HttpContext http, string name) => Guid.TryParse(http.Request.Query[name].FirstOrDefault(), out var value) ? value : null;
    private static InventoryValuationQuery ParseValuationQuery(HttpContext http) => new(
        ParseGuid(http, "companyId") ?? Guid.Empty,
        ParseGuid(http, "branchId"),
        ParseGuid(http, "warehouseId"),
        ParseGuid(http, "productId"),
        ParseGuid(http, "unitOfMeasureId"),
        http.Request.Query["trackingIdentity"].FirstOrDefault(),
        Enum.TryParse<InventoryValuationEventStatus>(http.Request.Query["status"].FirstOrDefault(), true, out var status) ? status : null,
        long.TryParse(http.Request.Query["fromSequence"].FirstOrDefault(), out var fromSequence) ? fromSequence : null,
        long.TryParse(http.Request.Query["toSequence"].FirstOrDefault(), out var toSequence) ? toSequence : null,
        DateOnly.TryParse(http.Request.Query["effectiveFrom"].FirstOrDefault(), out var effectiveFrom) ? effectiveFrom : null,
        DateOnly.TryParse(http.Request.Query["effectiveTo"].FirstOrDefault(), out var effectiveTo) ? effectiveTo : null,
        Enum.TryParse<InventoryMovementSourceType>(http.Request.Query["sourceType"].FirstOrDefault(), true, out var sourceType) ? sourceType : null,
        ParseGuid(http, "policyId"),
        http.Request.Query["currency"].FirstOrDefault());
    private static T? ParseEnum<T>(HttpContext http, string name) where T : struct, Enum => Enum.TryParse<T>(http.Request.Query[name].FirstOrDefault(), true, out var value) ? value : null;
    private static bool ParseBool(HttpContext http, string name) => bool.TryParse(http.Request.Query[name].FirstOrDefault(), out var value) && value;
    private static Guid ParseRouteGuid(HttpContext http, string name) => Guid.TryParse(http.Request.RouteValues[name]?.ToString(), out var value) ? value : Guid.Empty;
    private static string? Key(HttpContext http) => http.Request.Headers["Idempotency-Key"].FirstOrDefault();
    private static byte[]? ReadVersion(HttpContext http)
    {
        var value = http.Request.Headers.IfMatch.FirstOrDefault(); if (string.IsNullOrWhiteSpace(value)) return null; value = value.Trim(); if (value.StartsWith("W/", StringComparison.OrdinalIgnoreCase)) return null; if (value.Length > 1 && value[0] == '"' && value[^1] == '"') value = value[1..^1]; try { var result = Convert.FromBase64String(value); return result.Length is > 0 and <= 64 ? result : null; } catch (FormatException) { return null; }
    }
    private static string Correlation(HttpContext http) => http.Items.TryGetValue(FoundationApiKeys.CorrelationItem, out var value) && value is string correlation ? correlation : FoundationCorrelation.Resolve(http.Request);
    private static async Task<bool> Antiforgery(HttpContext http) { try { await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http); return true; } catch (AntiforgeryValidationException) { return false; } }
    private static Task<IResult> Problem(HttpContext http, int status, string code, string operationId) => Task.FromResult<IResult>(Results.Problem(statusCode: status, title: status == 403 ? "Access denied" : "Inventory operation failed", detail: "The operation is not available or could not be completed.", type: $"https://api.minierp.local/problems/{code}", extensions: new Dictionary<string, object?> { ["code"] = code, ["correlationId"] = Correlation(http), ["operationId"] = operationId }));
}

#pragma warning restore CS1591
