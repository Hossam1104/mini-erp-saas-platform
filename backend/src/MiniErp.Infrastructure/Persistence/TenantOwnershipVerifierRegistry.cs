using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Infrastructure.Persistence.Modules.MasterData;

namespace MiniErp.Infrastructure.Persistence;

/// <summary>
/// Internal registration for the one stored-owner verifier associated with a
/// concrete mapped ITenantOwned CLR type.
/// </summary>
internal sealed class TenantOwnershipVerifierRegistration
{
    internal TenantOwnershipVerifierRegistration(
        Type entityType,
        Func<TenantPersistenceDbContext, EntityEntry, TenantId?> readStoredTenantId,
        Func<TenantPersistenceDbContext, EntityEntry, CancellationToken, Task<TenantId?>> readStoredTenantIdAsync)
    {
        EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
        ReadStoredTenantId = readStoredTenantId ?? throw new ArgumentNullException(nameof(readStoredTenantId));
        ReadStoredTenantIdAsync = readStoredTenantIdAsync
            ?? throw new ArgumentNullException(nameof(readStoredTenantIdAsync));

        if (!typeof(ITenantOwned).IsAssignableFrom(entityType) || entityType.IsAbstract)
        {
            throw new ArgumentException(
                "A verifier registration must target a concrete ITenantOwned type.",
                nameof(entityType));
        }
    }

    internal Type EntityType { get; }

    internal Func<TenantPersistenceDbContext, EntityEntry, TenantId?> ReadStoredTenantId { get; }

    internal Func<TenantPersistenceDbContext, EntityEntry, CancellationToken, Task<TenantId?>>
        ReadStoredTenantIdAsync { get; }
}

/// <summary>
/// Immutable, infrastructure-only registry for stored-owner verification.
/// </summary>
internal sealed class TenantOwnershipVerifierRegistry
{
    private readonly IReadOnlyDictionary<Type, TenantOwnershipVerifierRegistration> _registrations;

    internal TenantOwnershipVerifierRegistry(
        IEnumerable<TenantOwnershipVerifierRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        var registrationList = registrations.ToArray();
        var duplicate = registrationList
            .GroupBy(registration => registration.EntityType)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Duplicate tenant ownership verifier registration for '{duplicate.Key.FullName}'.");
        }

        _registrations = new ReadOnlyDictionary<Type, TenantOwnershipVerifierRegistration>(
            registrationList.ToDictionary(registration => registration.EntityType));
    }

    internal static TenantOwnershipVerifierRegistry CreateDefault()
    {
        return new TenantOwnershipVerifierRegistry(
        [
            new TenantOwnershipVerifierRegistration(
                typeof(TenantOwnedRecord),
                TenantOwnershipStoreVerifier.ReadStoredTenantId,
                TenantOwnershipStoreVerifier.ReadStoredTenantIdAsync)
        ]);
    }

    internal static TenantOwnershipVerifierRegistry CreateMasterData()
    {
        return new TenantOwnershipVerifierRegistry(
        [
            new TenantOwnershipVerifierRegistration(
                typeof(TenantOwnedRecord),
                TenantOwnershipStoreVerifier.ReadStoredTenantId,
                TenantOwnershipStoreVerifier.ReadStoredTenantIdAsync),
            MasterDataTenantOwnershipVerifier.For<MasterDataCategoryEntity>(),
            MasterDataTenantOwnershipVerifier.For<MasterDataUnitOfMeasureEntity>(),
            MasterDataTenantOwnershipVerifier.For<MasterDataConversionEntity>(),
            MasterDataTenantOwnershipVerifier.For<MasterDataProductEntity>(),
            MasterDataTenantOwnershipVerifier.For<MasterDataProductBarcodeEntity>(),
            MasterDataTenantOwnershipVerifier.For<MasterDataAuditEventEntity>()
        ]);
    }

    internal bool TryGet(Type entityType, out TenantOwnershipVerifierRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return _registrations.TryGetValue(entityType, out registration!);
    }

    internal void ValidateModel(IModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var mappedTenantOwnedTypes = model.GetEntityTypes()
            .Select(entityType => entityType.ClrType)
            .Where(type => type is not null
                && !type.IsAbstract
                && typeof(ITenantOwned).IsAssignableFrom(type))
            .Distinct()
            .ToArray();

        var missing = mappedTenantOwnedTypes
            .Where(type => !_registrations.ContainsKey(type))
            .Select(type => type.FullName ?? type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var orphaned = _registrations.Keys
            .Where(type => !mappedTenantOwnedTypes.Contains(type))
            .Select(type => type.FullName ?? type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        if (missing.Length != 0 || orphaned.Length != 0)
        {
            throw new InvalidOperationException(
                "Tenant ownership verifier registry does not exactly match the concrete mapped "
                + $"ITenantOwned types. Missing: [{string.Join(", ", missing)}]. "
                + $"Orphaned: [{string.Join(", ", orphaned)}].");
        }
    }
}
