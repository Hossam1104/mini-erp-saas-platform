using System.Reflection;
using Microsoft.AspNetCore.Http;
using MiniErp.Api;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.BusinessParties;
using MiniErp.Contracts.Modules.MasterData;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class MasterDataFailureClassificationTests
{
    [Theory]
    [InlineData("permission_unavailable", StatusCodes.Status503ServiceUnavailable)]
    [InlineData("scope_policy_unavailable", StatusCodes.Status503ServiceUnavailable)]
    [InlineData("approval_policy_unavailable", StatusCodes.Status503ServiceUnavailable)]
    [InlineData("resource_policy_unavailable", StatusCodes.Status503ServiceUnavailable)]
    [InlineData("authorization_operation_unmapped", StatusCodes.Status503ServiceUnavailable)]
    [InlineData("permission_denied", StatusCodes.Status403Forbidden)]
    [InlineData("resource_scope_denied", StatusCodes.Status403Forbidden)]
    [InlineData("cross_tenant_target_denied", StatusCodes.Status403Forbidden)]
    [InlineData("authorization_denied", StatusCodes.Status403Forbidden)]
    [InlineData("approval_required", StatusCodes.Status403Forbidden)]
    [InlineData("approval_pending", StatusCodes.Status403Forbidden)]
    [InlineData("approval_rejected", StatusCodes.Status403Forbidden)]
    [InlineData("approval_policy_not_configured", StatusCodes.Status403Forbidden)]
    [InlineData("approval_identity_missing", StatusCodes.Status403Forbidden)]
    [InlineData("approval_policy_invalid", StatusCodes.Status403Forbidden)]
    [InlineData("resource_policy_not_configured", StatusCodes.Status403Forbidden)]
    [InlineData("self_approval_denied", StatusCodes.Status403Forbidden)]
    [InlineData("product_duplicate", StatusCodes.Status409Conflict)]
    [InlineData("product_barcode_duplicate", StatusCodes.Status409Conflict)]
    public void Product_endpoint_preserves_failure_classification(string code, int expectedStatus)
    {
        Assert.Equal(
            expectedStatus,
            InvokeFailure(
                typeof(ProductIdentityEndpoints),
                typeof(ProductIdentityRecord),
                code));
    }

    [Theory]
    [InlineData("permission_unavailable", StatusCodes.Status503ServiceUnavailable)]
    [InlineData("scope_policy_unavailable", StatusCodes.Status503ServiceUnavailable)]
    [InlineData("approval_policy_unavailable", StatusCodes.Status503ServiceUnavailable)]
    [InlineData("resource_policy_unavailable", StatusCodes.Status503ServiceUnavailable)]
    [InlineData("authorization_operation_unmapped", StatusCodes.Status503ServiceUnavailable)]
    [InlineData("permission_denied", StatusCodes.Status403Forbidden)]
    [InlineData("resource_scope_denied", StatusCodes.Status403Forbidden)]
    [InlineData("cross_tenant_target_denied", StatusCodes.Status403Forbidden)]
    [InlineData("authorization_denied", StatusCodes.Status403Forbidden)]
    [InlineData("approval_required", StatusCodes.Status403Forbidden)]
    [InlineData("approval_pending", StatusCodes.Status403Forbidden)]
    [InlineData("approval_rejected", StatusCodes.Status403Forbidden)]
    [InlineData("approval_policy_not_configured", StatusCodes.Status403Forbidden)]
    [InlineData("approval_identity_missing", StatusCodes.Status403Forbidden)]
    [InlineData("approval_policy_invalid", StatusCodes.Status403Forbidden)]
    [InlineData("resource_policy_not_configured", StatusCodes.Status403Forbidden)]
    [InlineData("self_approval_denied", StatusCodes.Status403Forbidden)]
    [InlineData("supplier_duplicate", StatusCodes.Status409Conflict)]
    [InlineData("supplier_code_duplicate", StatusCodes.Status409Conflict)]
    [InlineData("supplier_registration_duplicate", StatusCodes.Status409Conflict)]
    public void Supplier_endpoint_preserves_failure_classification(string code, int expectedStatus)
    {
        Assert.Equal(
            expectedStatus,
            InvokeFailure(
                typeof(SupplierEndpoints),
                typeof(SupplierRecord),
                code));
    }

    [Theory]
    [InlineData("permission_unavailable", StatusCodes.Status503ServiceUnavailable)]
    [InlineData("permission_denied", StatusCodes.Status403Forbidden)]
    [InlineData("customer_code_duplicate", StatusCodes.Status409Conflict)]
    public void Customer_endpoint_reference_classification_remains_unchanged(
        string code,
        int expectedStatus)
    {
        Assert.Equal(
            expectedStatus,
            InvokeFailure(
                typeof(CustomerEndpoints),
                typeof(CustomerRecord),
                code));
    }

    private static int InvokeFailure(Type endpointType, Type valueType, string code)
    {
        var resultType = typeof(MasterDataOperationResult<>).MakeGenericType(valueType);
        var failure = resultType
            .GetMethod("Failure", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, new object?[] { code, null });
        var endpointMethod = endpointType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => method.Name == "ToResult" && method.IsGenericMethodDefinition)
            .MakeGenericMethod(valueType);
        var result = Assert.IsAssignableFrom<IResult>(endpointMethod.Invoke(
            null,
            new object?[]
            {
                new DefaultHttpContext(),
                failure,
                "test.operation",
                null,
                StatusCodes.Status200OK,
                false
            }));
        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        return statusResult.StatusCode ?? StatusCodes.Status500InternalServerError;
    }
}
