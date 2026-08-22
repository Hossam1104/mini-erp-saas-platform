#pragma warning disable CS1591

using System.Text.Json;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Inventory;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.App.Modules.Inventory;

public sealed partial class InventoryService
{
    public async Task<InventoryOperationResult<IReadOnlyList<InventoryReasonCodeRecord>>> ListReasonCodesAsync(
        InventoryRequestContext context,
        InventoryReasonCategory? category,
        bool includeInactive,
        Guid? warehouseId,
        Guid? companyId,
        Guid? branchId,
        CancellationToken cancellationToken = default)
    {
        var catalogue = ResolveCatalogueScope(context, warehouseId, companyId, branchId);
        if (catalogue is null || !authorization.IsAllowed(context, "inventory.reason.list", catalogue))
            return InventoryOperationResult<IReadOnlyList<InventoryReasonCodeRecord>>.Failure("forbidden");
        try
        {
            return InventoryOperationResult<IReadOnlyList<InventoryReasonCodeRecord>>.Success(
                await persistence.ListReasonCodesAsync(context, category, includeInactive, cancellationToken));
        }
        catch (InvalidOperationException) { return InventoryOperationResult<IReadOnlyList<InventoryReasonCodeRecord>>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryReasonCodeRecord>> FindReasonCodeAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        var catalogue = ResolveCatalogueScope(context, null, null, null);
        if (catalogue is null || !authorization.IsAllowed(context, "inventory.reason.read", catalogue))
            return InventoryOperationResult<InventoryReasonCodeRecord>.Failure("forbidden");
        try
        {
            var value = await persistence.FindReasonCodeAsync(context, id, cancellationToken);
            return value is null
                ? InventoryOperationResult<InventoryReasonCodeRecord>.Failure("not_found")
                : InventoryOperationResult<InventoryReasonCodeRecord>.Success(value);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryReasonCodeRecord>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryReasonCodeRecord>> CreateReasonCodeAsync(InventoryRequestContext context, InventoryReasonCodeCreateRequest request, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        var catalogue = ResolveCatalogueScope(context, null, null, null);
        if (catalogue is null || !authorization.IsAllowed(context, "inventory.reason.create", catalogue))
            return InventoryOperationResult<InventoryReasonCodeRecord>.Failure("forbidden");
        var code = NormalizeRequired(request.Code, 128).ToUpperInvariant();
        var english = NormalizeRequired(request.EnglishName, 256);
        var arabic = NormalizeRequired(request.ArabicName, 256);
        if (code.Length == 0 || english.Length == 0 || arabic.Length == 0 || !Enum.IsDefined(request.Category))
            return InventoryOperationResult<InventoryReasonCodeRecord>.Failure("validation_failed");
        var command = new InventoryReasonCodeCommand(Guid.NewGuid(), code, english, arabic, request.Category, context.ActorId, DateTimeOffset.UtcNow,
            context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"), Normalize(idempotencyKey, 256), InventoryFingerprints.Create(request));
        try
        {
            var value = await persistence.CreateReasonCodeAsync(context, command, cancellationToken);
            return value is null ? InventoryOperationResult<InventoryReasonCodeRecord>.Failure("duplicate_or_conflict") : InventoryOperationResult<InventoryReasonCodeRecord>.Success(value);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryReasonCodeRecord>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryReasonCodeRecord>> UpdateReasonCodeAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, InventoryReasonCodeUpdateRequest request, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        var catalogue = ResolveCatalogueScope(context, null, null, null);
        if (catalogue is null || !authorization.IsAllowed(context, "inventory.reason.update", catalogue))
            return InventoryOperationResult<InventoryReasonCodeRecord>.Failure("forbidden");
        if (expectedVersion is null or { Length: 0 }) return InventoryOperationResult<InventoryReasonCodeRecord>.Failure("validation_failed");
        var command = new InventoryReasonCodeUpdateCommand(id, expectedVersion, NormalizeRequired(request.EnglishName, 256), NormalizeRequired(request.ArabicName, 256), request.Category, request.IsActive, context.ActorId, DateTimeOffset.UtcNow,
            context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"), Normalize(idempotencyKey, 256), InventoryFingerprints.Create(new { id, expectedVersion, request }));
        try
        {
            var value = await persistence.UpdateReasonCodeAsync(context, command, cancellationToken);
            return value is null ? InventoryOperationResult<InventoryReasonCodeRecord>.Failure("conflict") : InventoryOperationResult<InventoryReasonCodeRecord>.Success(value);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryReasonCodeRecord>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<IReadOnlyList<InventoryAdjustmentRecord>>> ListAdjustmentsAsync(InventoryRequestContext context, Guid? warehouseId, Guid? companyId, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var scope = await ResolveOptionalScopeAsync(context, "inventory.adjustment.list", warehouseId, companyId, branchId, cancellationToken);
        if (!scope.Succeeded) return InventoryOperationResult<IReadOnlyList<InventoryAdjustmentRecord>>.Failure(scope.Code);
        try { return InventoryOperationResult<IReadOnlyList<InventoryAdjustmentRecord>>.Success(await persistence.ListAdjustmentsAsync(context, scope.Value, cancellationToken)); }
        catch (InvalidOperationException) { return InventoryOperationResult<IReadOnlyList<InventoryAdjustmentRecord>>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryAdjustmentRecord>> FindAdjustmentAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await persistence.FindAdjustmentAsync(context, id, cancellationToken);
            if (value is null) return InventoryOperationResult<InventoryAdjustmentRecord>.Failure("not_found");
            return authorization.IsAllowed(context, "inventory.adjustment.read", ControlScope(context, value.CompanyId, value.BranchId, value.WarehouseId))
                ? InventoryOperationResult<InventoryAdjustmentRecord>.Success(value)
                : InventoryOperationResult<InventoryAdjustmentRecord>.Failure("forbidden");
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryAdjustmentRecord>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryAdjustmentRecord>> CreateAdjustmentAsync(InventoryRequestContext context, InventoryAdjustmentCreateRequest request, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (request.Lines is null || request.Lines.Count == 0 || request.Lines.Count > 1000) return InventoryOperationResult<InventoryAdjustmentRecord>.Failure("lines_required");
        var scope = await ResolveScopeAsync(context, "inventory.adjustment.create", request.WarehouseId, request.CompanyId, request.BranchId, cancellationToken);
        if (!scope.Succeeded) return InventoryOperationResult<InventoryAdjustmentRecord>.Failure(scope.Code);
        var reasons = await LoadReasonsAsync(context, InventoryReasonCategory.Adjustment, cancellationToken);
        var lines = new List<InventoryAdjustmentLineCommand>(request.Lines.Count);
        foreach (var item in request.Lines)
        {
            if (item.ProductId == Guid.Empty || item.UnitOfMeasureId == Guid.Empty || item.Quantity <= 0m || !Enum.IsDefined(item.Direction)) return InventoryOperationResult<InventoryAdjustmentRecord>.Failure("validation_failed");
            var product = await products.FindAsync(context, item.ProductId, cancellationToken);
            var tracking = NormalizeTracking(item.TrackingIdentity);
            var valid = ValidateProduct(product, item.UnitOfMeasureId, tracking);
            if (!valid.Succeeded) return InventoryOperationResult<InventoryAdjustmentRecord>.Failure(valid.Code);
            var reason = reasons.FirstOrDefault(value => string.Equals(value.Code, NormalizeRequired(item.ReasonCode, 128).ToUpperInvariant(), StringComparison.Ordinal));
            if (reason is null) return InventoryOperationResult<InventoryAdjustmentRecord>.Failure("reason_code_invalid");
            lines.Add(new InventoryAdjustmentLineCommand(Guid.NewGuid(), item.ProductId, item.UnitOfMeasureId, item.Direction, item.Quantity, tracking ?? string.Empty, Normalize(item.EvidenceReference, 512), product!, reason));
        }
        var command = new InventoryAdjustmentCreateCommand(Guid.NewGuid(), scope.Value!, scope.Warehouse!.Code, scope.Warehouse.Name, Normalize(request.EvidenceReference, 512), lines, context.ActorId, DateTimeOffset.UtcNow,
            context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"), Normalize(idempotencyKey, 256), InventoryFingerprints.Create(request));
        try
        {
            var value = await persistence.CreateAdjustmentAsync(context, command, cancellationToken);
            return value is null ? InventoryOperationResult<InventoryAdjustmentRecord>.Failure("duplicate_or_conflict") : InventoryOperationResult<InventoryAdjustmentRecord>.Success(value);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryAdjustmentRecord>.Failure("persistence_unavailable"); }
    }

    public Task<InventoryOperationResult<InventoryAdjustmentRecord>> SubmitAdjustmentAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, string? reason, string? idempotencyKey, CancellationToken cancellationToken = default) =>
        ActAdjustmentAsync(context, id, expectedVersion, reason, idempotencyKey, "inventory.adjustment.submit", async (command, scope, current) =>
        {
            var policy = await approvalPolicyProvider.ResolveAsync(context, scope, "stock-adjustment", command.OccurredAt, cancellationToken);
            return await persistence.SubmitAdjustmentAsync(context, command, policy is not null, policy is null ? null : JsonSerializer.Serialize(policy, InventoryControlJson.Options), cancellationToken);
        }, cancellationToken);

    public Task<InventoryOperationResult<InventoryAdjustmentRecord>> ApproveAdjustmentAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, string? reason, string? idempotencyKey, CancellationToken cancellationToken = default) =>
        ActAdjustmentApprovalAsync(context, id, expectedVersion, reason, idempotencyKey, "inventory.adjustment.approve", cancellationToken);

    public Task<InventoryOperationResult<InventoryAdjustmentRecord>> RejectAdjustmentAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, string? reason, bool returnForChange, string? idempotencyKey, CancellationToken cancellationToken = default) =>
        ActAdjustmentAsync(context, id, expectedVersion, reason, idempotencyKey, returnForChange ? "inventory.adjustment.return" : "inventory.adjustment.reject", (command, _, _) => persistence.RejectAdjustmentAsync(context, command, returnForChange, cancellationToken), cancellationToken);

    public Task<InventoryOperationResult<InventoryAdjustmentRecord>> PostAdjustmentAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, string? reason, string? idempotencyKey, CancellationToken cancellationToken = default) =>
        ActAdjustmentAsync(context, id, expectedVersion, reason, idempotencyKey, "inventory.adjustment.post", (command, _, _) => persistence.PostAdjustmentAsync(context, command, cancellationToken), cancellationToken);

    public async Task<InventoryOperationResult<IReadOnlyList<InventoryCountRecord>>> ListCountsAsync(InventoryRequestContext context, Guid? warehouseId, Guid? companyId, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var scope = await ResolveOptionalScopeAsync(context, "inventory.count.list", warehouseId, companyId, branchId, cancellationToken);
        if (!scope.Succeeded) return InventoryOperationResult<IReadOnlyList<InventoryCountRecord>>.Failure(scope.Code);
        try { return InventoryOperationResult<IReadOnlyList<InventoryCountRecord>>.Success(await persistence.ListCountsAsync(context, scope.Value, cancellationToken)); }
        catch (InvalidOperationException) { return InventoryOperationResult<IReadOnlyList<InventoryCountRecord>>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryCountRecord>> FindCountAsync(InventoryRequestContext context, Guid id, bool counterView, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await persistence.FindCountAsync(context, id, !counterView, cancellationToken);
            if (value is null) return InventoryOperationResult<InventoryCountRecord>.Failure("not_found");
            var allowed = authorization.IsAllowed(context, counterView ? "inventory.count.counter.read" : "inventory.count.read", ControlScope(context, value.CompanyId, value.BranchId, value.WarehouseId));
            return allowed ? InventoryOperationResult<InventoryCountRecord>.Success(value) : InventoryOperationResult<InventoryCountRecord>.Failure("forbidden");
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryCountRecord>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryCountRecord>> CreateCountAsync(InventoryRequestContext context, InventoryCountCreateRequest request, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.CountType) || request.AssignedCounterId == Guid.Empty || request.ReviewerId == request.AssignedCounterId) return InventoryOperationResult<InventoryCountRecord>.Failure("validation_failed");
        var scope = await ResolveScopeAsync(context, "inventory.count.create", request.WarehouseId, request.CompanyId, request.BranchId, cancellationToken);
        if (!scope.Succeeded) return InventoryOperationResult<InventoryCountRecord>.Failure(scope.Code);
        var lineRequests = request.Lines ?? [];
        if (request.CountType == InventoryCountType.Cycle && lineRequests.Count == 0) return InventoryOperationResult<InventoryCountRecord>.Failure("lines_required");
        if (lineRequests.Count > 5000) return InventoryOperationResult<InventoryCountRecord>.Failure("too_many_lines");
        var selected = new List<InventoryCountLineRequest>(lineRequests);
        if (request.CountType == InventoryCountType.Full)
        {
            var movements = await persistence.ListMovementsAsync(context, scope.Value, null, cancellationToken);
            selected = movements
                .GroupBy(item => new { item.ProductId, item.UnitOfMeasureId, Tracking = item.TrackingIdentity ?? string.Empty })
                .Select(group => new InventoryCountLineRequest(group.Key.ProductId, group.Key.UnitOfMeasureId, string.IsNullOrEmpty(group.Key.Tracking) ? null : group.Key.Tracking))
                .ToList();
        }
        if (selected.Count == 0) return InventoryOperationResult<InventoryCountRecord>.Failure("lines_required");
        var lines = new List<InventoryCountLineCommand>(selected.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in selected)
        {
            if (item.ProductId == Guid.Empty || item.UnitOfMeasureId == Guid.Empty) return InventoryOperationResult<InventoryCountRecord>.Failure("validation_failed");
            var product = await products.FindAsync(context, item.ProductId, cancellationToken);
            var tracking = NormalizeTracking(item.TrackingIdentity);
            var valid = ValidateProduct(product, item.UnitOfMeasureId, tracking);
            if (!valid.Succeeded) return InventoryOperationResult<InventoryCountRecord>.Failure(valid.Code);
            var key = $"{item.ProductId:N}|{item.UnitOfMeasureId:N}|{tracking ?? string.Empty}";
            if (!seen.Add(key)) return InventoryOperationResult<InventoryCountRecord>.Failure("duplicate_line");
            lines.Add(new InventoryCountLineCommand(Guid.NewGuid(), null, 1, item.ProductId, item.UnitOfMeasureId, tracking ?? string.Empty, 0m, product!));
        }
        var command = new InventoryCountCreateCommand(Guid.NewGuid(), scope.Value!, scope.Warehouse!.Code, scope.Warehouse.Name, request.CountType, request.AssignedCounterId, request.ReviewerId, lines, DateTimeOffset.UtcNow,
            context.ActorId, DateTimeOffset.UtcNow, context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"), Normalize(idempotencyKey, 256), InventoryFingerprints.Create(request));
        try
        {
            var value = await persistence.CreateCountAsync(context, command, cancellationToken);
            return value is null ? InventoryOperationResult<InventoryCountRecord>.Failure("duplicate_or_conflict") : InventoryOperationResult<InventoryCountRecord>.Success(value);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryCountRecord>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryCountRecord>> SubmitCountAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, InventoryCountSubmitRequest request, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (request.Observations is null || request.Observations.Count == 0) return InventoryOperationResult<InventoryCountRecord>.Failure("observations_required");
        var current = await persistence.FindCountAsync(context, id, true, cancellationToken);
        if (current is null) return InventoryOperationResult<InventoryCountRecord>.Failure("not_found");
        var scope = ControlScope(context, current.CompanyId, current.BranchId, current.WarehouseId);
        if (!authorization.IsAllowed(context, "inventory.count.submit", scope)) return InventoryOperationResult<InventoryCountRecord>.Failure("forbidden");
        if (context.ActorId != current.AssignedCounterId) return InventoryOperationResult<InventoryCountRecord>.Failure("counter_required");
        if (expectedVersion is null or { Length: 0 }) return InventoryOperationResult<InventoryCountRecord>.Failure("validation_failed");
        var command = new InventoryCountSubmitCommand(id, expectedVersion, request.Observations, context.ActorId, Normalize(idempotencyKey, 256), InventoryFingerprints.Create(new { id, expectedVersion, request }), context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow);
        try
        {
            var value = await persistence.SubmitCountAsync(context, command, cancellationToken);
            return value is null ? InventoryOperationResult<InventoryCountRecord>.Failure("conflict") : InventoryOperationResult<InventoryCountRecord>.Success(value);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryCountRecord>.Failure("persistence_unavailable"); }
    }

    public Task<InventoryOperationResult<InventoryCountRecord>> ApproveCountAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, string? reason, string? idempotencyKey, CancellationToken cancellationToken = default) =>
        ActCountApprovalAsync(context, id, expectedVersion, reason, idempotencyKey, cancellationToken);

    public Task<InventoryOperationResult<InventoryCountRecord>> RejectCountAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, string? reason, bool returnForChange, string? idempotencyKey, CancellationToken cancellationToken = default) =>
        ActCountAsync(context, id, expectedVersion, reason, idempotencyKey, returnForChange ? "inventory.count.return" : "inventory.count.reject", (command, _) => persistence.RejectCountAsync(context, command, returnForChange, cancellationToken), cancellationToken);

    public Task<InventoryOperationResult<InventoryCountRecord>> RequestCountRecountAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, string? reason, string? idempotencyKey, CancellationToken cancellationToken = default) =>
        ActCountAsync(context, id, expectedVersion, reason, idempotencyKey, "inventory.count.recount", (command, _) => persistence.RequestCountRecountAsync(context, command, cancellationToken), cancellationToken);

    public Task<InventoryOperationResult<InventoryCountRecord>> ResnapshotCountAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, string? reason, string? idempotencyKey, CancellationToken cancellationToken = default) =>
        ActCountAsync(context, id, expectedVersion, reason, idempotencyKey, "inventory.count.resnapshot", (command, _) => persistence.ResnapshotCountAsync(context, command, cancellationToken), cancellationToken);

    public Task<InventoryOperationResult<InventoryCountRecord>> PostCountAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, string? reason, string? idempotencyKey, CancellationToken cancellationToken = default) =>
        ActCountAsync(context, id, expectedVersion, reason, idempotencyKey, "inventory.count.post", (command, current) => current.AssignedCounterId == context.ActorId || current.ApproverId == context.ActorId
            ? Task.FromResult<InventoryCountRecord?>(null)
            : persistence.PostCountAsync(context, command, cancellationToken), cancellationToken);

    public async Task<InventoryOperationResult<IReadOnlyList<InventoryStockIssueRecord>>> ListStockIssuesAsync(InventoryRequestContext context, Guid? warehouseId, Guid? companyId, Guid? branchId, CancellationToken cancellationToken = default)
    {
        var scope = await ResolveOptionalScopeAsync(context, "inventory.issue.list", warehouseId, companyId, branchId, cancellationToken);
        if (!scope.Succeeded) return InventoryOperationResult<IReadOnlyList<InventoryStockIssueRecord>>.Failure(scope.Code);
        try { return InventoryOperationResult<IReadOnlyList<InventoryStockIssueRecord>>.Success(await persistence.ListStockIssuesAsync(context, scope.Value, cancellationToken)); }
        catch (InvalidOperationException) { return InventoryOperationResult<IReadOnlyList<InventoryStockIssueRecord>>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryStockIssueRecord>> FindStockIssueAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await persistence.FindStockIssueAsync(context, id, cancellationToken);
            if (value is null) return InventoryOperationResult<InventoryStockIssueRecord>.Failure("not_found");
            return authorization.IsAllowed(context, "inventory.issue.read", ControlScope(context, value.CompanyId, value.BranchId, value.WarehouseId))
                ? InventoryOperationResult<InventoryStockIssueRecord>.Success(value)
                : InventoryOperationResult<InventoryStockIssueRecord>.Failure("forbidden");
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryStockIssueRecord>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryStockIssueRecord>> CreateStockIssueAsync(InventoryRequestContext context, InventoryStockIssueCreateRequest request, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DestinationUseDescription) || request.Lines is null || request.Lines.Count == 0 || request.Lines.Count > 1000) return InventoryOperationResult<InventoryStockIssueRecord>.Failure("validation_failed");
        var scope = await ResolveScopeAsync(context, "inventory.issue.create", request.WarehouseId, request.CompanyId, request.BranchId, cancellationToken);
        if (!scope.Succeeded) return InventoryOperationResult<InventoryStockIssueRecord>.Failure(scope.Code);
        var reasons = await LoadReasonsAsync(context, InventoryReasonCategory.StockIssue, cancellationToken);
        var lines = new List<InventoryStockIssueLineCommand>(request.Lines.Count);
        foreach (var item in request.Lines)
        {
            if (item.ProductId == Guid.Empty || item.UnitOfMeasureId == Guid.Empty || item.Quantity <= 0m) return InventoryOperationResult<InventoryStockIssueRecord>.Failure("validation_failed");
            var product = await products.FindAsync(context, item.ProductId, cancellationToken);
            var tracking = NormalizeTracking(item.TrackingIdentity);
            var valid = ValidateProduct(product, item.UnitOfMeasureId, tracking);
            if (!valid.Succeeded) return InventoryOperationResult<InventoryStockIssueRecord>.Failure(valid.Code);
            var reason = reasons.FirstOrDefault(value => string.Equals(value.Code, NormalizeRequired(item.ReasonCode, 128).ToUpperInvariant(), StringComparison.Ordinal));
            if (reason is null) return InventoryOperationResult<InventoryStockIssueRecord>.Failure("reason_code_invalid");
            lines.Add(new InventoryStockIssueLineCommand(Guid.NewGuid(), item.ProductId, item.UnitOfMeasureId, item.Quantity, tracking ?? string.Empty, Normalize(item.EvidenceReference, 512), product!, reason));
        }
        var command = new InventoryStockIssueCreateCommand(Guid.NewGuid(), scope.Value!, scope.Warehouse!.Code, scope.Warehouse.Name, NormalizeRequired(request.DestinationUseDescription, 2048), lines, context.ActorId, DateTimeOffset.UtcNow,
            context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"), Normalize(idempotencyKey, 256), InventoryFingerprints.Create(request));
        try
        {
            var value = await persistence.CreateStockIssueAsync(context, command, cancellationToken);
            return value is null ? InventoryOperationResult<InventoryStockIssueRecord>.Failure("duplicate_or_conflict") : InventoryOperationResult<InventoryStockIssueRecord>.Success(value);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryStockIssueRecord>.Failure("persistence_unavailable"); }
    }

    public Task<InventoryOperationResult<InventoryStockIssueRecord>> SubmitStockIssueAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, string? reason, string? idempotencyKey, CancellationToken cancellationToken = default) =>
        ActIssueAsync(context, id, expectedVersion, reason, idempotencyKey, "inventory.issue.submit", async (command, scope, _) =>
        {
            var policy = await approvalPolicyProvider.ResolveAsync(context, scope, "stock-issue", command.OccurredAt, cancellationToken);
            return await persistence.SubmitStockIssueAsync(context, command, policy is not null, policy is null ? null : JsonSerializer.Serialize(policy, InventoryControlJson.Options), cancellationToken);
        }, cancellationToken);

    public Task<InventoryOperationResult<InventoryStockIssueRecord>> ApproveStockIssueAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, string? reason, string? idempotencyKey, CancellationToken cancellationToken = default) =>
        ActIssueApprovalAsync(context, id, expectedVersion, reason, idempotencyKey, cancellationToken);

    public Task<InventoryOperationResult<InventoryStockIssueRecord>> RejectStockIssueAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, string? reason, bool returnForChange, string? idempotencyKey, CancellationToken cancellationToken = default) =>
        ActIssueAsync(context, id, expectedVersion, reason, idempotencyKey, returnForChange ? "inventory.issue.return" : "inventory.issue.reject", (command, _, _) => persistence.RejectStockIssueAsync(context, command, returnForChange, cancellationToken), cancellationToken);

    public Task<InventoryOperationResult<InventoryStockIssueRecord>> PostStockIssueAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, string? reason, string? idempotencyKey, CancellationToken cancellationToken = default) =>
        ActIssueAsync(context, id, expectedVersion, reason, idempotencyKey, "inventory.issue.post", (command, _, _) => persistence.PostStockIssueAsync(context, command, cancellationToken), cancellationToken);

    public async Task<InventoryOperationResult<IReadOnlyList<InventoryControlHistoryRecord>>> ReadControlHistoryAsync(InventoryRequestContext context, string resourceType, Guid id, CancellationToken cancellationToken = default)
    {
        InventoryScope? scope;
        string operation;
        if (resourceType == "adjustment")
        {
            var value = await FindAdjustmentAsync(context, id, cancellationToken);
            if (!value.Succeeded) return InventoryOperationResult<IReadOnlyList<InventoryControlHistoryRecord>>.Failure(value.Code);
            scope = ControlScope(context, value.Value!.CompanyId, value.Value.BranchId, value.Value.WarehouseId);
            operation = "inventory.adjustment.history.read";
        }
        else if (resourceType == "count")
        {
            var value = await FindCountAsync(context, id, false, cancellationToken);
            if (!value.Succeeded) return InventoryOperationResult<IReadOnlyList<InventoryControlHistoryRecord>>.Failure(value.Code);
            scope = ControlScope(context, value.Value!.CompanyId, value.Value.BranchId, value.Value.WarehouseId);
            operation = "inventory.count.history.read";
        }
        else if (resourceType == "stock-issue")
        {
            var value = await FindStockIssueAsync(context, id, cancellationToken);
            if (!value.Succeeded) return InventoryOperationResult<IReadOnlyList<InventoryControlHistoryRecord>>.Failure(value.Code);
            scope = ControlScope(context, value.Value!.CompanyId, value.Value.BranchId, value.Value.WarehouseId);
            operation = "inventory.issue.history.read";
        }
        else return InventoryOperationResult<IReadOnlyList<InventoryControlHistoryRecord>>.Failure("validation_failed");
        if (scope is null || !authorization.IsAllowed(context, operation, scope)) return InventoryOperationResult<IReadOnlyList<InventoryControlHistoryRecord>>.Failure("forbidden");
        try { return InventoryOperationResult<IReadOnlyList<InventoryControlHistoryRecord>>.Success(await persistence.ReadControlHistoryAsync(context, resourceType, id, cancellationToken)); }
        catch (InvalidOperationException) { return InventoryOperationResult<IReadOnlyList<InventoryControlHistoryRecord>>.Failure("persistence_unavailable"); }
    }

    public async Task<InventoryOperationResult<InventoryMovementRecord>> CorrectMovementAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, InventoryCorrectionRequest request, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        var movement = await persistence.FindMovementAsync(context, id, cancellationToken);
        if (movement is null) return InventoryOperationResult<InventoryMovementRecord>.Failure("not_found");
        if (movement.SourceType is not (InventoryMovementSourceType.StockAdjustment or InventoryMovementSourceType.InventoryCountVariance or InventoryMovementSourceType.StockIssue)) return InventoryOperationResult<InventoryMovementRecord>.Failure("correction_not_allowed");
        var scope = ControlScope(context, movement.CompanyId, movement.BranchId, movement.WarehouseId);
        if (!authorization.IsAllowed(context, "inventory.movement.correct", scope)) return InventoryOperationResult<InventoryMovementRecord>.Failure("forbidden");
        var reasons = await LoadReasonsAsync(context, InventoryReasonCategory.Adjustment, cancellationToken);
        var reason = reasons.FirstOrDefault(value => string.Equals(value.Code, NormalizeRequired(request.ReasonCode, 128).ToUpperInvariant(), StringComparison.Ordinal));
        if (reason is null || expectedVersion is null or { Length: 0 }) return InventoryOperationResult<InventoryMovementRecord>.Failure("reason_code_invalid");
        var command = new InventoryMovementCorrectionCommand(id, expectedVersion, context.ActorId, reason.Id, reason.Code, reason.EnglishName, reason.ArabicName, Normalize(request.Reason, 2048), context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"), Normalize(idempotencyKey, 256), InventoryFingerprints.Create(new { id, expectedVersion, request }), DateTimeOffset.UtcNow);
        try
        {
            var value = await persistence.CorrectMovementAsync(context, command, cancellationToken);
            return value is null ? InventoryOperationResult<InventoryMovementRecord>.Failure("conflict") : InventoryOperationResult<InventoryMovementRecord>.Success(value);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryMovementRecord>.Failure("persistence_unavailable"); }
    }

    private async Task<IReadOnlyList<InventoryReasonCodeRecord>> LoadReasonsAsync(InventoryRequestContext context, InventoryReasonCategory category, CancellationToken cancellationToken) =>
        await persistence.ListReasonCodesAsync(context, category, false, cancellationToken);

    private async Task<InventoryOperationResult<InventoryAdjustmentRecord>> ActAdjustmentAsync(
        InventoryRequestContext context, Guid id, byte[] expectedVersion, string? reason, string? idempotencyKey, string operationId,
        Func<InventoryControlActionCommand, InventoryScope, InventoryAdjustmentRecord, Task<InventoryAdjustmentRecord?>> action, CancellationToken cancellationToken)
    {
        var current = await persistence.FindAdjustmentAsync(context, id, cancellationToken);
        if (current is null) return InventoryOperationResult<InventoryAdjustmentRecord>.Failure("not_found");
        var scope = ControlScope(context, current.CompanyId, current.BranchId, current.WarehouseId);
        if (!authorization.IsAllowed(context, operationId, scope)) return InventoryOperationResult<InventoryAdjustmentRecord>.Failure("forbidden");
        if (expectedVersion is null or { Length: 0 }) return InventoryOperationResult<InventoryAdjustmentRecord>.Failure("validation_failed");
        var command = ActionCommand(context, id, expectedVersion, reason, idempotencyKey, new { id, expectedVersion, reason });
        try
        {
            var value = await action(command, scope, current);
            return value is null ? InventoryOperationResult<InventoryAdjustmentRecord>.Failure("conflict") : InventoryOperationResult<InventoryAdjustmentRecord>.Success(value);
        }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryAdjustmentRecord>.Failure("persistence_unavailable"); }
    }

    private async Task<InventoryOperationResult<InventoryAdjustmentRecord>> ActAdjustmentApprovalAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, string? reason, string? idempotencyKey, string operationId, CancellationToken cancellationToken)
    {
        var current = await persistence.FindAdjustmentAsync(context, id, cancellationToken);
        if (current is null) return InventoryOperationResult<InventoryAdjustmentRecord>.Failure("not_found");
        var scope = ControlScope(context, current.CompanyId, current.BranchId, current.WarehouseId);
        if (!authorization.IsAllowed(context, operationId, scope)) return InventoryOperationResult<InventoryAdjustmentRecord>.Failure("forbidden");
        var approval = await ResolveApprovalActorAsync(context, scope, "stock-adjustment", current.RequesterId, current.Approval, cancellationToken);
        if (!approval.Succeeded) return InventoryOperationResult<InventoryAdjustmentRecord>.Failure(approval.Code);
        var command = ActionCommand(context, id, expectedVersion, reason, idempotencyKey, new { id, expectedVersion, reason });
        command = command with { DelegatedFromActorId = approval.DelegatedFromActorId };
        try { var value = await persistence.ApproveAdjustmentAsync(context, command, cancellationToken); return value is null ? InventoryOperationResult<InventoryAdjustmentRecord>.Failure("conflict") : InventoryOperationResult<InventoryAdjustmentRecord>.Success(value); }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryAdjustmentRecord>.Failure("persistence_unavailable"); }
    }

    private async Task<InventoryOperationResult<InventoryCountRecord>> ActCountAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, string? reason, string? idempotencyKey, string operationId, Func<InventoryControlActionCommand, InventoryCountRecord, Task<InventoryCountRecord?>> action, CancellationToken cancellationToken)
    {
        var current = await persistence.FindCountAsync(context, id, true, cancellationToken);
        if (current is null) return InventoryOperationResult<InventoryCountRecord>.Failure("not_found");
        var scope = ControlScope(context, current.CompanyId, current.BranchId, current.WarehouseId);
        if (!authorization.IsAllowed(context, operationId, scope)) return InventoryOperationResult<InventoryCountRecord>.Failure("forbidden");
        if (expectedVersion is null or { Length: 0 }) return InventoryOperationResult<InventoryCountRecord>.Failure("validation_failed");
        var command = ActionCommand(context, id, expectedVersion, reason, idempotencyKey, new { id, expectedVersion, reason });
        try { var value = await action(command, current); if (value is null) return InventoryOperationResult<InventoryCountRecord>.Failure("conflict"); if (value.Status == InventoryControlDocumentStatus.ResnapshotRequired) return InventoryOperationResult<InventoryCountRecord>.Failure("resnapshot_required"); if (value.Status == InventoryControlDocumentStatus.Blocked) return InventoryOperationResult<InventoryCountRecord>.Failure("reservation_reconciliation_required"); return InventoryOperationResult<InventoryCountRecord>.Success(value); }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryCountRecord>.Failure("persistence_unavailable"); }
    }

    private async Task<InventoryOperationResult<InventoryCountRecord>> ActCountApprovalAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, string? reason, string? idempotencyKey, CancellationToken cancellationToken)
    {
        var current = await persistence.FindCountAsync(context, id, true, cancellationToken);
        if (current is null) return InventoryOperationResult<InventoryCountRecord>.Failure("not_found");
        var scope = ControlScope(context, current.CompanyId, current.BranchId, current.WarehouseId);
        if (!authorization.IsAllowed(context, "inventory.count.approve", scope)) return InventoryOperationResult<InventoryCountRecord>.Failure("forbidden");
        if (context.ActorId == current.AssignedCounterId || context.ActorId == current.ReviewerId) return InventoryOperationResult<InventoryCountRecord>.Failure("separation_of_duties_required");
        var command = ActionCommand(context, id, expectedVersion, reason, idempotencyKey, new { id, expectedVersion, reason });
        try { var value = await persistence.ApproveCountAsync(context, command, cancellationToken); return value is null ? InventoryOperationResult<InventoryCountRecord>.Failure("conflict") : InventoryOperationResult<InventoryCountRecord>.Success(value); }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryCountRecord>.Failure("persistence_unavailable"); }
    }

    private async Task<InventoryOperationResult<InventoryStockIssueRecord>> ActIssueAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, string? reason, string? idempotencyKey, string operationId, Func<InventoryControlActionCommand, InventoryScope, InventoryStockIssueRecord, Task<InventoryStockIssueRecord?>> action, CancellationToken cancellationToken)
    {
        var current = await persistence.FindStockIssueAsync(context, id, cancellationToken);
        if (current is null) return InventoryOperationResult<InventoryStockIssueRecord>.Failure("not_found");
        var scope = ControlScope(context, current.CompanyId, current.BranchId, current.WarehouseId);
        if (!authorization.IsAllowed(context, operationId, scope)) return InventoryOperationResult<InventoryStockIssueRecord>.Failure("forbidden");
        if (expectedVersion is null or { Length: 0 }) return InventoryOperationResult<InventoryStockIssueRecord>.Failure("validation_failed");
        var command = ActionCommand(context, id, expectedVersion, reason, idempotencyKey, new { id, expectedVersion, reason });
        try { var value = await action(command, scope, current); return value is null ? InventoryOperationResult<InventoryStockIssueRecord>.Failure("conflict") : InventoryOperationResult<InventoryStockIssueRecord>.Success(value); }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryStockIssueRecord>.Failure("persistence_unavailable"); }
    }

    private async Task<InventoryOperationResult<InventoryStockIssueRecord>> ActIssueApprovalAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, string? reason, string? idempotencyKey, CancellationToken cancellationToken)
    {
        var current = await persistence.FindStockIssueAsync(context, id, cancellationToken);
        if (current is null) return InventoryOperationResult<InventoryStockIssueRecord>.Failure("not_found");
        var scope = ControlScope(context, current.CompanyId, current.BranchId, current.WarehouseId);
        if (!authorization.IsAllowed(context, "inventory.issue.approve", scope)) return InventoryOperationResult<InventoryStockIssueRecord>.Failure("forbidden");
        var approval = await ResolveApprovalActorAsync(context, scope, "stock-issue", current.RequesterId, current.Approval, cancellationToken);
        if (!approval.Succeeded) return InventoryOperationResult<InventoryStockIssueRecord>.Failure(approval.Code);
        var command = ActionCommand(context, id, expectedVersion, reason, idempotencyKey, new { id, expectedVersion, reason }) with { DelegatedFromActorId = approval.DelegatedFromActorId };
        try { var value = await persistence.ApproveStockIssueAsync(context, command, cancellationToken); return value is null ? InventoryOperationResult<InventoryStockIssueRecord>.Failure("conflict") : InventoryOperationResult<InventoryStockIssueRecord>.Success(value); }
        catch (InvalidOperationException) { return InventoryOperationResult<InventoryStockIssueRecord>.Failure("persistence_unavailable"); }
    }

    private async Task<ApprovalActorResolution> ResolveApprovalActorAsync(InventoryRequestContext context, InventoryScope scope, string documentType, Guid requesterId, InventoryApprovalRecord? current, CancellationToken cancellationToken)
    {
        if (requesterId == context.ActorId) return ApprovalActorResolution.Failure("self_approval_denied");
        var policy = await approvalPolicyProvider.ResolveAsync(context, scope, documentType, DateTimeOffset.UtcNow, cancellationToken);
        if (policy is null) return ApprovalActorResolution.Failure("approval_not_required");
        var stage = policy.Stages.OrderBy(item => item.Sequence).ElementAtOrDefault(current?.StageIndex ?? 0);
        if (stage is null) return ApprovalActorResolution.Failure("approval_policy_invalid");
        if (stage.EligibleApproverIds is null || stage.EligibleApproverIds.Count == 0 || stage.EligibleApproverIds.Contains(context.ActorId)) return ApprovalActorResolution.Success(null);
        if (!stage.AllowDelegation) return ApprovalActorResolution.Failure("approver_not_eligible");
        var delegation = await approvalDelegationProvider.ResolveAsync(context, scope, stage, context.ActorId, DateTimeOffset.UtcNow, cancellationToken);
        return delegation is null ? ApprovalActorResolution.Failure("approver_not_eligible") : ApprovalActorResolution.Success(delegation.DelegatorId);
    }

    private static InventoryControlActionCommand ActionCommand(InventoryRequestContext context, Guid id, byte[] expectedVersion, string? reason, string? idempotencyKey, object fingerprint) =>
        new(id, expectedVersion, context.ActorId, Normalize(reason, 2048), null, context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"), Normalize(idempotencyKey, 256), InventoryFingerprints.Create(fingerprint), DateTimeOffset.UtcNow);

    private static InventoryScope ControlScope(InventoryRequestContext context, Guid companyId, Guid? branchId, Guid warehouseId) => new(context.TenantId.Value, companyId, branchId, warehouseId);

    private static InventoryScope? ResolveCatalogueScope(InventoryRequestContext context, Guid? warehouseId, Guid? companyId, Guid? branchId)
    {
        if (warehouseId.HasValue && companyId.HasValue) return new InventoryScope(context.TenantId.Value, companyId.Value, branchId, warehouseId.Value);
        if (context.TrustedScope is not { } trusted) return new InventoryScope(context.TenantId.Value, Guid.Empty, null, Guid.Empty);
        var parts = trusted.Value.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && Guid.TryParse(parts[1], out var id))
            return string.Equals(parts[0], "Warehouse", StringComparison.OrdinalIgnoreCase)
                ? new InventoryScope(context.TenantId.Value, Guid.Empty, null, id)
                : string.Equals(parts[0], "Company", StringComparison.OrdinalIgnoreCase)
                    ? new InventoryScope(context.TenantId.Value, id, Guid.Empty, Guid.Empty)
                    : new InventoryScope(context.TenantId.Value, Guid.Empty, null, Guid.Empty);
        return new InventoryScope(context.TenantId.Value, Guid.Empty, null, Guid.Empty);
    }

    private sealed record ApprovalActorResolution(bool Succeeded, string Code, Guid? DelegatedFromActorId)
    {
        internal static ApprovalActorResolution Success(Guid? delegatedFrom) => new(true, "eligible", delegatedFrom);
        internal static ApprovalActorResolution Failure(string code) => new(false, code, null);
    }
}

#pragma warning restore CS1591
