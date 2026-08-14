#pragma warning disable CS1591

using Microsoft.Extensions.DependencyInjection;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.MasterData;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class MasterDataAuthorizationTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Actor = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Session = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid Company = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Theory]
    [InlineData("tenant.master-data.price-list.view", MasterDataCapability.View)]
    [InlineData("tenant.master-data.price-list.create", MasterDataCapability.Create)]
    [InlineData("tenant.master-data.price-list.edit", MasterDataCapability.Edit)]
    [InlineData("tenant.master-data.price-list.deactivate", MasterDataCapability.Deactivate)]
    [InlineData("tenant.master-data.price-list.activate", MasterDataCapability.Activate)]
    [InlineData("tenant.master-data.price-list.audit", MasterDataCapability.ViewAuditHistory)]
    public void Exact_foundation_permission_grants_only_its_capability(
        string permission,
        MasterDataCapability expected)
    {
        var resolver = new FoundationPermissionMasterDataCapabilityResolver();

        var capabilities = resolver.Resolve(Context(permission));

        Assert.Equal([expected], capabilities);
    }

    [Fact]
    public void All_current_master_data_foundation_permissions_have_explicit_capability_mapping()
    {
        var resolver = new FoundationPermissionMasterDataCapabilityResolver();

        foreach (var operation in FoundationOperationCatalog.PublicOperations.Where(item =>
                     item.OperationId.StartsWith("master-data.", StringComparison.Ordinal)))
        {
            Assert.False(string.IsNullOrWhiteSpace(operation.ExactPermissionCode), operation.OperationId);
            Assert.True(
                MasterDataOperationCatalog.TryGetCapabilityForPermission(operation.ExactPermissionCode, out var expected),
                operation.OperationId);

            var capabilities = resolver.Resolve(Context(operation.ExactPermissionCode!));

            Assert.Equal([expected], capabilities);
        }
    }

    [Fact]
    public void Unknown_unrelated_and_non_tenant_contexts_fail_closed()
    {
        var resolver = new FoundationPermissionMasterDataCapabilityResolver();

        Assert.Empty(resolver.Resolve(Context("tenant.master-data.price-list.unknown")));
        Assert.Empty(resolver.Resolve(Context("tenant.foundation.target.read")));

        var authenticatedOnly = new MasterDataTenantContextResolver().Resolve(
            FoundationRequestContext.ForAuthenticatedSession(Actor, Session));
        Assert.False(authenticatedOnly.Allowed);
        Assert.Throws<ArgumentNullException>(() => resolver.Resolve(null!));
    }

    [Fact]
    public void Production_master_data_composition_does_not_register_granting_resolver()
    {
        var services = new ServiceCollection();
        services.AddMasterDataAuthorization();

        var registrations = services
            .Where(descriptor => descriptor.ServiceType == typeof(IMasterDataCapabilityResolver))
            .ToArray();

        var registration = Assert.Single(registrations);
        Assert.Equal(typeof(FoundationPermissionMasterDataCapabilityResolver), registration.ImplementationType);
        Assert.DoesNotContain(registrations, descriptor => descriptor.ImplementationType == typeof(GrantingMasterDataCapabilityResolver));
    }

    private static MasterDataRequestContext Context(string permission)
    {
        var tenantContext = TenantContext.ForOrdinaryMembership(
            new TenantId(Tenant),
            new MembershipReference(Guid.Parse("77777777-7777-7777-7777-777777777777")),
            new ScopeReference($"Company:{Company:D}"),
            new CorrelationId("master-data-auth"),
            Actor);

        return Assert.IsType<MasterDataRequestContext>(
            new MasterDataTenantContextResolver().Resolve(
                FoundationRequestContext.ForTenant(Actor, Session, tenantContext, permission)).Context);
    }
}

#pragma warning restore CS1591
