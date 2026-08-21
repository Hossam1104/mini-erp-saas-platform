#pragma warning disable CS1591

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
            return Results.Json(result.Value);
        }
        var status = result.Code is "forbidden" or "tenant_context_failed" or "warehouse_not_available" ? 403 : result.Code is "not_found" ? 404 : result.Code is "persistence_unavailable" ? 503 : result.Code is "conflict" or "duplicate_or_conflict" or "insufficient_available" or "idempotency_conflict" ? 409 : 400;
        return Results.Problem(statusCode: status, title: status == 403 ? "Access denied" : "Inventory operation failed", detail: "The Inventory operation could not be completed.", type: $"https://api.minierp.local/problems/{result.Code}", extensions: new Dictionary<string, object?> { ["code"] = result.Code, ["correlationId"] = Correlation(http), ["operationId"] = operationId });
    }

    private static Guid? ParseGuid(HttpContext http, string name) => Guid.TryParse(http.Request.Query[name].FirstOrDefault(), out var value) ? value : null;
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
