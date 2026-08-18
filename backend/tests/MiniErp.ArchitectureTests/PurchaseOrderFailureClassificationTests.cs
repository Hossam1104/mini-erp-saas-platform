using System.Reflection;
using Microsoft.AspNetCore.Http;
using MiniErp.Api;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Procurement;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class PurchaseOrderFailureClassificationTests
{
    [Theory]
    [InlineData("creator_only", StatusCodes.Status403Forbidden)]
    [InlineData("self_approval_denied", StatusCodes.Status403Forbidden)]
    [InlineData("approval_not_eligible", StatusCodes.Status403Forbidden)]
    [InlineData("approval_duplicate", StatusCodes.Status409Conflict)]
    [InlineData("purchase_order_duplicate", StatusCodes.Status409Conflict)]
    [InlineData("proposed_quantity_below_confirmed", StatusCodes.Status409Conflict)]
    [InlineData("validation_failed", StatusCodes.Status400BadRequest)]
    public void Purchase_order_endpoint_preserves_business_failure_classification(string code, int expectedStatus)
    {
        var resultType = typeof(PurchaseOrderOperationResult<>).MakeGenericType(typeof(PurchaseOrderRecord));
        var failure = resultType
            .GetMethod("Failure", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [code]);
        var endpointMethod = typeof(PurchaseOrderEndpoints)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => method.Name == "ToResult" && method.IsGenericMethodDefinition)
            .MakeGenericMethod(typeof(PurchaseOrderRecord));

        var result = Assert.IsAssignableFrom<IResult>(endpointMethod.Invoke(
            null,
            [
                new DefaultHttpContext(),
                failure,
                "test.operation",
                null,
                null,
                false
            ]));
        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(expectedStatus, statusResult.StatusCode);
    }
}
