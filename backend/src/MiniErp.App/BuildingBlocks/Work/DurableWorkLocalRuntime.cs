#pragma warning disable CS1591

namespace MiniErp.App.BuildingBlocks.Work;

/// <summary>
/// The single approved local composition owner for the Foundation
/// durable-work seam. <see cref="Create"/> is the only shipping entry point
/// that may construct <see cref="InMemoryDurableWorkEffectGuard"/>,
/// <see cref="DurableWorkEffectExecutor"/>, <see cref="InMemoryDurableWorkStore"/>
/// and <see cref="DurableWorkDispatcher"/>: it builds exactly one guard, one
/// executor, one store and one dispatcher, and supplies the identical
/// executor instance to both the store and the dispatcher so every protected
/// effect for a Tenant -- handler or outbox -- is guarded by one ledger.
/// Every constructor this runtime calls is internal to this assembly, so no
/// other shipping code can build an independent store or dispatcher with a
/// different executor; a syntax-tree architecture test in the test project
/// additionally proves no other shipping construction site exists. Each call
/// to <see cref="Create"/> starts a new,
/// independent runtime with its own single-ledger authority: an application
/// composition root must call it exactly once and reuse the returned
/// instance.
/// </summary>
/// <remarks>
/// H92-05: the mutable ledger (<see cref="EffectGuard"/>) and the effect
/// executor (<see cref="EffectExecutor"/>) are internal composition details,
/// not part of this runtime's public surface. A shipping caller holding this
/// runtime can reach only <see cref="Store"/> and <see cref="Dispatcher"/>;
/// it cannot directly reserve, release, complete or mark an effect uncertain,
/// because doing so through the shared ledger outside
/// <see cref="DurableWorkEffectExecutor"/> would let a duplicate reservation
/// or premature release defeat the single-effect guarantee (for example,
/// releasing an in-flight reservation while its protected effect is still
/// executing, then reserving and executing it a second time).
/// </remarks>
public sealed class DurableWorkLocalRuntime
{
    private DurableWorkLocalRuntime(
        IDurableWorkEffectGuard effectGuard,
        IDurableWorkEffectExecutor effectExecutor,
        InMemoryDurableWorkStore store,
        DurableWorkDispatcher dispatcher)
    {
        EffectGuard = effectGuard;
        EffectExecutor = effectExecutor;
        Store = store;
        Dispatcher = dispatcher;
    }

    /// <summary>
    /// The one authoritative effect ledger shared by <see cref="Store"/> and
    /// <see cref="Dispatcher"/>. Internal (H92-05): no shipping caller outside
    /// this assembly's approved effect executor may reserve, release,
    /// complete or mark an effect uncertain. Tests reach this only through
    /// InternalsVisibleTo.
    /// </summary>
    internal IDurableWorkEffectGuard EffectGuard { get; }

    /// <summary>
    /// The one effect executor bound to <see cref="EffectGuard"/> and shared
    /// by <see cref="Store"/> and <see cref="Dispatcher"/>. Internal
    /// (H92-05): a shipping caller must go through <see cref="Store"/> or
    /// <see cref="Dispatcher"/>, never invoke a protected effect directly.
    /// </summary>
    internal IDurableWorkEffectExecutor EffectExecutor { get; }

    /// <summary>The one durable-work store constructed with <see cref="EffectExecutor"/>.</summary>
    public InMemoryDurableWorkStore Store { get; }

    /// <summary>The one handler dispatcher constructed with the identical <see cref="EffectExecutor"/> instance.</summary>
    public DurableWorkDispatcher Dispatcher { get; }

    /// <summary>
    /// The single approved composition entry point. Constructs one guard, one
    /// executor bound to it, and a store and dispatcher that both receive
    /// that exact executor instance -- so the same-purpose effect can never
    /// execute once per independent ledger.
    /// </summary>
    public static DurableWorkLocalRuntime Create(
        IDurableWorkOperationCatalogue operationCatalogue,
        IDurableWorkPayloadRegistry payloadRegistry)
    {
        ArgumentNullException.ThrowIfNull(operationCatalogue);
        ArgumentNullException.ThrowIfNull(payloadRegistry);

        var effectGuard = new InMemoryDurableWorkEffectGuard();
        var effectExecutor = new DurableWorkEffectExecutor(effectGuard);
        var store = new InMemoryDurableWorkStore(effectExecutor);
        var dispatcher = new DurableWorkDispatcher(operationCatalogue, payloadRegistry, effectExecutor);
        return new DurableWorkLocalRuntime(effectGuard, effectExecutor, store, dispatcher);
    }
}
