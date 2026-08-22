using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Procurement;

namespace MiniErp.ArchitectureTests;

internal sealed class NoActiveGoodsReceiptInventoryEffectReader : IGoodsReceiptInventoryEffectReader
{
    public Task<GoodsReceiptInventoryEffectVerification> VerifyAsync(TenantContext tenantContext, Guid goodsReceiptId, CancellationToken cancellationToken = default) =>
        Task.FromResult(GoodsReceiptInventoryEffectVerification.NoActiveEffect);
}

internal sealed class ActiveGoodsReceiptInventoryEffectReader : IGoodsReceiptInventoryEffectReader
{
    public Task<GoodsReceiptInventoryEffectVerification> VerifyAsync(TenantContext tenantContext, Guid goodsReceiptId, CancellationToken cancellationToken = default) =>
        Task.FromResult(GoodsReceiptInventoryEffectVerification.ActiveEffectExists);
}

internal sealed class ThrowingGoodsReceiptInventoryEffectReader : IGoodsReceiptInventoryEffectReader
{
    public Task<GoodsReceiptInventoryEffectVerification> VerifyAsync(TenantContext tenantContext, Guid goodsReceiptId, CancellationToken cancellationToken = default) =>
        Task.FromException<GoodsReceiptInventoryEffectVerification>(new InvalidOperationException("test provider unavailable"));
}
