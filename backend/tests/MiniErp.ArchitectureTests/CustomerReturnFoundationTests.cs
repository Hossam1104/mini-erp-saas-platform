using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Infrastructure.Persistence.Modules.Finance;
using MiniErp.Infrastructure.Persistence.Modules.Inventory;
using MiniErp.Infrastructure.Persistence.Modules.Sales;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class CustomerReturnFoundationTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public void Phase_A_operations_are_catalogued_as_tenant_bound_mutations_or_reads()
    {
        var operationIds = new[]
        {
            "sales.customer-return.eligible-source.list",
            "sales.customer-return.eligible-source.read",
            "sales.customer-return.create",
            "sales.customer-return.read",
            "sales.customer-return.submit",
            "sales.customer-return.approve",
            "sales.customer-return.reject",
            "sales.customer-return.cancel",
            "sales.customer-return.reverse",
            "sales.customer-return.history.read",
            "sales.customer-return.audit.read",
            "inventory.customer-return.receive",
            "inventory.customer-return.inspect",
            "inventory.customer-return.read",
            "finance.credit-note.create",
            "finance.credit-note.read",
            "finance.credit-note.submit",
            "finance.credit-note.approve",
            "finance.credit-note.reject",
            "finance.credit-note.cancel",
            "finance.credit-note.post",
            "finance.credit-note.reverse"
        };

        foreach (var operationId in operationIds)
        {
            var operation = FoundationOperationCatalog.GetRequired(operationId);
            Assert.Equal(FoundationOperationVisibility.Public, operation.Visibility);
            Assert.Equal(FoundationSecurityProfile.OrdinaryMembership, operation.SecurityProfile);
            Assert.Equal(FoundationScopePolicy.Tenant, operation.ScopePolicy);

            if (operation.HttpMethod == "POST")
            {
                Assert.True(operation.RequiresAntiforgery);
                Assert.True(operation.RequiresMandatoryAudit);
                Assert.True(operation.IsUnsafe);
                Assert.Equal(FoundationIdempotencyPolicy.Required, operation.Idempotency);
            }
        }
    }

    [Fact]
    public void Each_module_owns_a_tenant_filtered_customer_return_model()
    {
        var options = new DbContextOptionsBuilder().UseSqlite("Data Source=:memory:").Options;

        using var sales = new SalesDbContext(options, Context("sales"));
        using var inventory = new InventoryDbContext(options, Context("inventory"));
        using var finance = new FinanceDbContext(options, Context("finance"));

        AssertTenantOwned(sales.Model, "MiniErp.Infrastructure.Persistence.Modules.Sales.SalesCustomerReturnEntity");
        AssertTenantOwned(sales.Model, "MiniErp.Infrastructure.Persistence.Modules.Sales.SalesCustomerReturnLineEntity");
        AssertTenantOwned(inventory.Model, "MiniErp.Infrastructure.Persistence.Modules.Inventory.InventoryCustomerReturnEntity");
        AssertTenantOwned(inventory.Model, "MiniErp.Infrastructure.Persistence.Modules.Inventory.InventoryCustomerReturnLineEntity");
        AssertTenantOwned(finance.Model, "MiniErp.Infrastructure.Persistence.Modules.Finance.FinanceCreditNoteEntity");
        AssertTenantOwned(finance.Model, "MiniErp.Infrastructure.Persistence.Modules.Finance.FinanceCreditNoteLineEntity");
        AssertTenantOwned(finance.Model, "MiniErp.Infrastructure.Persistence.Modules.Finance.FinanceCustomerCreditEntity");
        AssertTenantOwned(finance.Model, "MiniErp.Infrastructure.Persistence.Modules.Finance.FinanceCustomerCreditApplicationEntity");
    }

    private static void AssertTenantOwned(IModel model, string entityName)
    {
        var entity = model.GetEntityTypes().Single(item => item.ClrType.FullName == entityName);
        Assert.NotNull(entity.FindProperty("TenantId"));
        Assert.NotEmpty(entity.GetDeclaredQueryFilters());
        Assert.Contains(entity.GetIndexes(), index => index.Properties.Any(property => property.Name == "TenantId"));
    }

    private static TenantContext Context(string purpose) => TenantContext.ForOrdinaryMembership(
        new TenantId(TenantId),
        new MembershipReference(Guid.NewGuid()),
        correlationId: new CorrelationId($"return-{purpose}"));
}
