#pragma warning disable CS1591

using MiniErp.Contracts.Modules.Inventory;

namespace MiniErp.App.Modules.Inventory;

public sealed class InventoryService(
    IInventoryPersistence persistence,
    InventoryResourceAuthorizationService authorization,
    IInventoryWarehouseProvider warehouses,
    IInventoryProductProvider products)
{
    public async Task<InventoryOperationResult<IReadOnlyList<InventoryWarehouseOption>>> ListWarehousesAsync(
        InventoryRequestContext context,
        Guid? companyId,
        Guid? branchId,
        CancellationToken cancellationToken = default)
    {
        var result = await warehouses.ListAsync(context, companyId, branchId, cancellationToken);
        if (context.TrustedScope is null)
        {
            return InventoryOperationResult<IReadOnlyList<InventoryWarehouseOption>>.Success(result);
        }

        var filtered = result.Where(item => authorization.IsAllowed(
            context,
            "inventory.warehouse.list",
            new InventoryScope(context.TenantId.Value, item.CompanyId, item.BranchId, item.WarehouseId))).ToArray();
        return InventoryOperationResult<IReadOnlyList<InventoryWarehouseOption>>.Success(filtered);
    }

    public async Task<InventoryOperationResult<IReadOnlyList<InventoryMovementRecord>>> ListMovementsAsync(
        InventoryRequestContext context,
        Guid? warehouseId,
        Guid? companyId,
        Guid? branchId,
        Guid? productId,
        CancellationToken cancellationToken = default)
    {
        var scope = await ResolveOptionalScopeAsync(context, "inventory.ledger.read", warehouseId, companyId, branchId, cancellationToken);
        if (!scope.Succeeded) return InventoryOperationResult<IReadOnlyList<InventoryMovementRecord>>.Failure(scope.Code);
        try
        {
            return InventoryOperationResult<IReadOnlyList<InventoryMovementRecord>>.Success(
                await persistence.ListMovementsAsync(context, scope.Value, productId, cancellationToken));
        }
        catch (InvalidOperationException) { return InventoryOperationResult<IReadOnlyList<InventoryMovementRecord>>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryMovementRecord>> FindMovementAsync(
        InventoryRequestContext context, Guid movementId, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await persistence.FindMovementAsync(context, movementId, cancellationToken);
            if (value is null) return InventoryOperationResult<InventoryMovementRecord>.Failure("not_found");
            return !authorization.IsAllowed(context, "inventory.ledger.read", new InventoryScope(context.TenantId.Value, value.CompanyId, value.BranchId, value.WarehouseId))
                ? InventoryOperationResult<InventoryMovementRecord>.Failure("forbidden")
                : InventoryOperationResult<InventoryMovementRecord>.Success(value);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryMovementRecord>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryAvailabilityRecord>> GetAvailabilityAsync(
        InventoryRequestContext context,
        Guid warehouseId,
        Guid companyId,
        Guid? branchId,
        Guid productId,
        Guid unitOfMeasureId,
        string? trackingIdentity,
        CancellationToken cancellationToken = default)
    {
        var scope = await ResolveScopeAsync(context, "inventory.availability.read", warehouseId, companyId, branchId, cancellationToken);
        if (!scope.Succeeded) return InventoryOperationResult<InventoryAvailabilityRecord>.Failure(scope.Code);
        var warehouse = scope.Warehouse!;
        var product = await products.FindAsync(context, productId, cancellationToken);
        var productResult = ValidateProduct(product, unitOfMeasureId, trackingIdentity);
        if (!productResult.Succeeded) return InventoryOperationResult<InventoryAvailabilityRecord>.Failure(productResult.Code);
        try
        {
            var value = await persistence.GetAvailabilityAsync(context, scope.Value!, productId, unitOfMeasureId, NormalizeTracking(trackingIdentity), product!, warehouse, cancellationToken);
            return value is null
                ? InventoryOperationResult<InventoryAvailabilityRecord>.Failure("not_found")
                : InventoryOperationResult<InventoryAvailabilityRecord>.Success(value);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryAvailabilityRecord>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<IReadOnlyList<InventoryOpeningBalanceRecord>>> ListOpeningBalancesAsync(
        InventoryRequestContext context, Guid? warehouseId, Guid? companyId, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var scope = await ResolveOptionalScopeAsync(context, "inventory.opening.read", warehouseId, companyId, branchId, cancellationToken);
        if (!scope.Succeeded) return InventoryOperationResult<IReadOnlyList<InventoryOpeningBalanceRecord>>.Failure(scope.Code);
        try
        {
            return InventoryOperationResult<IReadOnlyList<InventoryOpeningBalanceRecord>>.Success(
                await persistence.ListOpeningBalancesAsync(context, scope.Value, cancellationToken));
        }
        catch (InvalidOperationException) { return InventoryOperationResult<IReadOnlyList<InventoryOpeningBalanceRecord>>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryOpeningBalanceRecord>> FindOpeningBalanceAsync(
        InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await persistence.FindOpeningBalanceAsync(context, id, cancellationToken);
            if (value is null) return InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("not_found");
            return !authorization.IsAllowed(context, "inventory.opening.read", new InventoryScope(context.TenantId.Value, value.CompanyId, value.BranchId, value.WarehouseId))
                ? InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("forbidden")
                : InventoryOperationResult<InventoryOpeningBalanceRecord>.Success(value);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryOpeningBalanceRecord>> CreateOpeningBalanceAsync(
        InventoryRequestContext context,
        InventoryOpeningBalanceCreateRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (request.Rows is null || request.Rows.Count == 0 || request.Rows.Count > 10000) return InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("rows_required");
        if (request.CompanyId == Guid.Empty || request.WarehouseId == Guid.Empty || string.IsNullOrWhiteSpace(request.SourceOwner) || string.IsNullOrWhiteSpace(request.SourceSystem)) return InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("invalid_opening_source");
        if (request.ExtractedAt > DateTimeOffset.UtcNow.AddMinutes(5)) return InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("invalid_extracted_at");
        var scope = await ResolveScopeAsync(context, "inventory.opening.create", request.WarehouseId, request.CompanyId, request.BranchId, cancellationToken);
        if (!scope.Succeeded) return InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure(scope.Code);

        var rows = new List<InventoryOpeningBalanceRowCommand>(request.Rows.Count);
        foreach (var row in request.Rows)
        {
            var product = await products.FindAsync(context, row.ProductId, cancellationToken);
            var validation = ValidateProduct(product, row.UnitOfMeasureId, row.TrackingIdentity);
            var code = validation.Succeeded ? ValidateOpeningRow(row) : validation.Code;
            rows.Add(new InventoryOpeningBalanceRowCommand(
                Guid.NewGuid(), row.ProductId, row.UnitOfMeasureId, row.Quantity, row.UnitCost,
                NormalizeCurrency(row.CurrencyCode), NormalizeTracking(row.TrackingIdentity),
                Normalize(row.SourceLineReference, 256), product, code));
        }

        var command = new InventoryOpeningBalanceCommand(
            Guid.NewGuid(), scope.Value!, scope.Warehouse!.Code, scope.Warehouse.Name, request.AsOfDate, NormalizeRequired(request.SourceOwner, 256),
            NormalizeRequired(request.SourceSystem, 256), request.ExtractedAt, Normalize(request.SourceReference, 512),
            rows, context.ActorId, DateTimeOffset.UtcNow, context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"),
            Normalize(idempotencyKey, 256), InventoryFingerprints.Create(request));
        try
        {
            var value = await persistence.CreateOpeningBalanceAsync(context, command, cancellationToken);
            return value is null ? InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("duplicate_or_conflict") : InventoryOperationResult<InventoryOpeningBalanceRecord>.Success(value);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("persistence_unavailable"); }
    }

    public Task<InventoryOperationResult<InventoryOpeningBalanceRecord>> ValidateOpeningBalanceAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, string? reason, string? idempotencyKey, CancellationToken cancellationToken = default) =>
        ActOpeningAsync(context, id, expectedVersion, reason, idempotencyKey, "inventory.opening.validate", persistence.ValidateOpeningBalanceAsync, cancellationToken);

    public Task<InventoryOperationResult<InventoryOpeningBalanceRecord>> PostOpeningBalanceAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, string? reason, string? idempotencyKey, CancellationToken cancellationToken = default) =>
        ActOpeningAsync(context, id, expectedVersion, reason, idempotencyKey, "inventory.opening.post", persistence.PostOpeningBalanceAsync, cancellationToken);

    public Task<InventoryOperationResult<InventoryOpeningBalanceRecord>> CorrectOpeningBalanceAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, string? reason, string? idempotencyKey, CancellationToken cancellationToken = default) =>
        ActOpeningAsync(context, id, expectedVersion, reason, idempotencyKey, "inventory.opening.correct", persistence.CorrectOpeningBalanceAsync, cancellationToken);

    private async Task<InventoryOperationResult<InventoryOpeningBalanceRecord>> ActOpeningAsync(
        InventoryRequestContext context,
        Guid id,
        byte[] expectedVersion,
        string? reason,
        string? idempotencyKey,
        string operationId,
        Func<InventoryRequestContext, Guid, byte[], Guid, string?, string, string?, string, CancellationToken, Task<InventoryOpeningBalanceRecord?>> action,
        CancellationToken cancellationToken)
    {
        var current = await persistence.FindOpeningBalanceAsync(context, id, cancellationToken);
        if (current is null) return InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("not_found");
        var scope = new InventoryScope(context.TenantId.Value, current.CompanyId, current.BranchId, current.WarehouseId);
        if (!authorization.IsAllowed(context, operationId, scope)) return InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("forbidden");
        try
        {
            var value = await action(context, id, expectedVersion, context.ActorId, Normalize(reason, 2048), context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"), Normalize(idempotencyKey, 256), InventoryFingerprints.Create(new { id, reason, expectedVersion }), cancellationToken);
            return value is null ? InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("conflict") : InventoryOperationResult<InventoryOpeningBalanceRecord>.Success(value);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryOpeningBalanceRecord>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<IReadOnlyList<InventoryOpeningBalanceHistoryRecord>>> ReadOpeningHistoryAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var opening = await persistence.FindOpeningBalanceAsync(context, id, cancellationToken);
            if (opening is null) return InventoryOperationResult<IReadOnlyList<InventoryOpeningBalanceHistoryRecord>>.Failure("not_found");
            if (!authorization.IsAllowed(context, "inventory.opening.history.read", new InventoryScope(context.TenantId.Value, opening.CompanyId, opening.BranchId, opening.WarehouseId))) return InventoryOperationResult<IReadOnlyList<InventoryOpeningBalanceHistoryRecord>>.Failure("forbidden");
            return InventoryOperationResult<IReadOnlyList<InventoryOpeningBalanceHistoryRecord>>.Success(await persistence.ReadOpeningHistoryAsync(context, id, cancellationToken));
        }
        catch (InvalidOperationException) { return InventoryOperationResult<IReadOnlyList<InventoryOpeningBalanceHistoryRecord>>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<IReadOnlyList<InventoryReservationRecord>>> ListReservationsAsync(InventoryRequestContext context, Guid? warehouseId, Guid? companyId, Guid? branchId, Guid? productId, CancellationToken cancellationToken = default)
    {
        var scope = await ResolveOptionalScopeAsync(context, "inventory.reservation.read", warehouseId, companyId, branchId, cancellationToken);
        if (!scope.Succeeded) return InventoryOperationResult<IReadOnlyList<InventoryReservationRecord>>.Failure(scope.Code);
        try { return InventoryOperationResult<IReadOnlyList<InventoryReservationRecord>>.Success(await persistence.ListReservationsAsync(context, scope.Value, productId, cancellationToken)); }
        catch (InvalidOperationException) { return InventoryOperationResult<IReadOnlyList<InventoryReservationRecord>>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryReservationRecord>> FindReservationAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await persistence.FindReservationAsync(context, id, cancellationToken);
            if (value is null) return InventoryOperationResult<InventoryReservationRecord>.Failure("not_found");
            return !authorization.IsAllowed(context, "inventory.reservation.read", new InventoryScope(context.TenantId.Value, value.CompanyId, value.BranchId, value.WarehouseId))
                ? InventoryOperationResult<InventoryReservationRecord>.Failure("forbidden")
                : InventoryOperationResult<InventoryReservationRecord>.Success(value);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryReservationRecord>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryReservationRecord>> CreateReservationAsync(InventoryRequestContext context, InventoryReservationCreateRequest request, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (request.RequestedQuantity <= 0 || string.IsNullOrWhiteSpace(request.SourceType) || string.IsNullOrWhiteSpace(request.SourceReference)) return InventoryOperationResult<InventoryReservationRecord>.Failure("invalid_reservation");
        var scope = await ResolveScopeAsync(context, "inventory.reservation.create", request.WarehouseId, request.CompanyId, request.BranchId, cancellationToken);
        if (!scope.Succeeded) return InventoryOperationResult<InventoryReservationRecord>.Failure(scope.Code);
        var product = await products.FindAsync(context, request.ProductId, cancellationToken);
        var productValidation = ValidateProduct(product, request.UnitOfMeasureId, request.TrackingIdentity);
        if (!productValidation.Succeeded) return InventoryOperationResult<InventoryReservationRecord>.Failure(productValidation.Code);
        try
        {
            var availability = await persistence.GetAvailabilityAsync(context, scope.Value!, request.ProductId, request.UnitOfMeasureId, NormalizeTracking(request.TrackingIdentity), product!, scope.Warehouse!, cancellationToken);
            var available = availability?.AvailableQuantity ?? 0;
            if (request.RequestedQuantity > available && !request.AllowPartialAllocation) return InventoryOperationResult<InventoryReservationRecord>.Failure("insufficient_available");
            var command = new InventoryReservationCommand(
                Guid.NewGuid(), scope.Value!, request.ProductId, request.UnitOfMeasureId, request.RequestedQuantity,
                NormalizeRequired(request.SourceType, 128), NormalizeRequired(request.SourceReference, 512), request.AllowPartialAllocation,
                NormalizeTracking(request.TrackingIdentity), product!, scope.Warehouse!.Code, scope.Warehouse.Name, context.ActorId,
                DateTimeOffset.UtcNow, context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"), Normalize(idempotencyKey, 256),
                InventoryFingerprints.Create(request));
            var value = await persistence.CreateReservationAsync(context, command, available, cancellationToken);
            return value is null ? InventoryOperationResult<InventoryReservationRecord>.Failure("conflict") : InventoryOperationResult<InventoryReservationRecord>.Success(value);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryReservationRecord>.Failure("persistence_unavailable"); }
    }

    public Task<InventoryOperationResult<InventoryReservationRecord>> ReduceReservationAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, decimal quantity, string? reason, string? idempotencyKey, CancellationToken cancellationToken = default) =>
        ActReservationAsync(context, id, expectedVersion, quantity, reason, idempotencyKey, "inventory.reservation.reduce", persistence.ReduceReservationAsync, cancellationToken);

    public Task<InventoryOperationResult<InventoryReservationRecord>> ReleaseReservationAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, string? reason, string? idempotencyKey, CancellationToken cancellationToken = default) =>
        ActReservationAsync(context, id, expectedVersion, null, reason, idempotencyKey, "inventory.reservation.release", (c, i, v, _, a, r, correlation, k, f, ct) => persistence.ReleaseReservationAsync(c, i, v, a, r, correlation, k, f, ct), cancellationToken);

    private async Task<InventoryOperationResult<InventoryReservationRecord>> ActReservationAsync(
        InventoryRequestContext context, Guid id, byte[] expectedVersion, decimal? quantity, string? reason, string? idempotencyKey, string operationId,
        Func<InventoryRequestContext, Guid, byte[], decimal, Guid, string?, string, string?, string, CancellationToken, Task<InventoryReservationRecord?>> action,
        CancellationToken cancellationToken)
    {
        var current = await persistence.FindReservationAsync(context, id, cancellationToken);
        if (current is null) return InventoryOperationResult<InventoryReservationRecord>.Failure("not_found");
        if (!authorization.IsAllowed(context, operationId, new InventoryScope(context.TenantId.Value, current.CompanyId, current.BranchId, current.WarehouseId))) return InventoryOperationResult<InventoryReservationRecord>.Failure("forbidden");
        if (quantity is <= 0) return InventoryOperationResult<InventoryReservationRecord>.Failure("invalid_quantity");
        try
        {
            var value = await action(context, id, expectedVersion, quantity ?? 0, context.ActorId, Normalize(reason, 2048), context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"), Normalize(idempotencyKey, 256), InventoryFingerprints.Create(new { id, quantity, reason, expectedVersion }), cancellationToken);
            return value is null ? InventoryOperationResult<InventoryReservationRecord>.Failure("conflict") : InventoryOperationResult<InventoryReservationRecord>.Success(value);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryReservationRecord>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<IReadOnlyList<InventoryReservationHistoryRecord>>> ReadReservationHistoryAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var reservation = await persistence.FindReservationAsync(context, id, cancellationToken);
            if (reservation is null) return InventoryOperationResult<IReadOnlyList<InventoryReservationHistoryRecord>>.Failure("not_found");
            if (!authorization.IsAllowed(context, "inventory.reservation.history.read", new InventoryScope(context.TenantId.Value, reservation.CompanyId, reservation.BranchId, reservation.WarehouseId))) return InventoryOperationResult<IReadOnlyList<InventoryReservationHistoryRecord>>.Failure("forbidden");
            return InventoryOperationResult<IReadOnlyList<InventoryReservationHistoryRecord>>.Success(await persistence.ReadReservationHistoryAsync(context, id, cancellationToken));
        }
        catch (InvalidOperationException) { return InventoryOperationResult<IReadOnlyList<InventoryReservationHistoryRecord>>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<IReadOnlyList<InventoryAuditRecord>>> ReadAuditAsync(InventoryRequestContext context, string resourceType, Guid resourceId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (resourceType.Equals("opening-balance", StringComparison.OrdinalIgnoreCase))
            {
                var opening = await persistence.FindOpeningBalanceAsync(context, resourceId, cancellationToken);
                if (opening is null) return InventoryOperationResult<IReadOnlyList<InventoryAuditRecord>>.Failure("not_found");
                if (!authorization.IsAllowed(context, "inventory.opening.audit.read", new InventoryScope(context.TenantId.Value, opening.CompanyId, opening.BranchId, opening.WarehouseId))) return InventoryOperationResult<IReadOnlyList<InventoryAuditRecord>>.Failure("forbidden");
            }
            else if (resourceType.Equals("reservation", StringComparison.OrdinalIgnoreCase))
            {
                var reservation = await persistence.FindReservationAsync(context, resourceId, cancellationToken);
                if (reservation is null) return InventoryOperationResult<IReadOnlyList<InventoryAuditRecord>>.Failure("not_found");
                if (!authorization.IsAllowed(context, "inventory.reservation.audit.read", new InventoryScope(context.TenantId.Value, reservation.CompanyId, reservation.BranchId, reservation.WarehouseId))) return InventoryOperationResult<IReadOnlyList<InventoryAuditRecord>>.Failure("forbidden");
            }
            else return InventoryOperationResult<IReadOnlyList<InventoryAuditRecord>>.Failure("invalid_resource");

            return InventoryOperationResult<IReadOnlyList<InventoryAuditRecord>>.Success(await persistence.ReadAuditAsync(context, resourceType, resourceId, cancellationToken));
        }
        catch (InvalidOperationException) { return InventoryOperationResult<IReadOnlyList<InventoryAuditRecord>>.Failure("persistence_unavailable"); }
    }

    private async Task<ScopeResolution> ResolveScopeAsync(InventoryRequestContext context, string operationId, Guid warehouseId, Guid companyId, Guid? branchId, CancellationToken cancellationToken)
    {
        if (warehouseId == Guid.Empty || companyId == Guid.Empty) return ScopeResolution.Failure("invalid_scope");
        var warehouse = await warehouses.FindAsync(context, warehouseId, cancellationToken);
        if (warehouse is null || !warehouse.IsActive || warehouse.CompanyId != companyId || warehouse.BranchId != branchId) return ScopeResolution.Failure("warehouse_not_available");
        var scope = new InventoryScope(context.TenantId.Value, companyId, branchId, warehouseId);
        return authorization.IsAllowed(context, operationId, scope) ? ScopeResolution.Success(scope, warehouse) : ScopeResolution.Failure("forbidden");
    }

    private async Task<ScopeResolution> ResolveOptionalScopeAsync(InventoryRequestContext context, string operationId, Guid? warehouseId, Guid? companyId, Guid? branchId, CancellationToken cancellationToken)
    {
        if (!warehouseId.HasValue)
        {
            return context.TrustedScope is null
                ? ScopeResolution.Success(null, null)
                : ScopeResolution.Failure("scope_required");
        }
        if (!companyId.HasValue) return ScopeResolution.Failure("company_required");
        return await ResolveScopeAsync(context, operationId, warehouseId.Value, companyId.Value, branchId, cancellationToken);
    }

    private static InventoryOperationResult<bool> ValidateProduct(InventoryProductReference? product, Guid unitOfMeasureId, string? trackingIdentity)
    {
        if (product is null) return InventoryOperationResult<bool>.Failure("product_not_available");
        if (!product.IsActive || !product.IsInventoryRelevant) return InventoryOperationResult<bool>.Failure("product_not_available");
        if (product.BaseUnitOfMeasureId != unitOfMeasureId) return InventoryOperationResult<bool>.Failure("unit_of_measure_not_supported");
        if (product.TrackingEnabled && string.IsNullOrWhiteSpace(trackingIdentity)) return InventoryOperationResult<bool>.Failure("tracking_identity_required");
        if (!product.TrackingEnabled && !string.IsNullOrWhiteSpace(trackingIdentity)) return InventoryOperationResult<bool>.Failure("tracking_not_enabled");
        return InventoryOperationResult<bool>.Success(true);
    }

    private static string? ValidateOpeningRow(InventoryOpeningBalanceRowRequest row)
    {
        if (row.Quantity <= 0) return "invalid_quantity";
        if (row.UnitCost < 0) return "invalid_unit_cost";
        if (string.IsNullOrWhiteSpace(row.CurrencyCode)) return "currency_required";
        return null;
    }

    private sealed record ScopeResolution(bool Succeeded, string Code, InventoryScope? Value, InventoryWarehouseOption? Warehouse)
    {
        internal static ScopeResolution Success(InventoryScope? scope, InventoryWarehouseOption? warehouse) => new(true, "resolved", scope, warehouse);
        internal static ScopeResolution Failure(string code) => new(false, code, null, null);
    }

    private static string? Normalize(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];
    private static string NormalizeRequired(string value, int max) => Normalize(value, max) ?? string.Empty;
    private static string NormalizeCurrency(string value) => NormalizeRequired(value, 16).ToUpperInvariant();
    private static string? NormalizeTracking(string? value) => Normalize(value, 256);
}

#pragma warning restore CS1591
