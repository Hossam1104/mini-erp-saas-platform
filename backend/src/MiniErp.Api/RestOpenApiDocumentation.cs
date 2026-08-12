#pragma warning disable CS1591

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using MiniErp.Contracts.Modules.Foundation;

namespace MiniErp.Api;

/// <summary>
/// Project-wide generated OpenAPI identity and boundary statement. The
/// runtime document remains generated from mapped endpoints; this transformer
/// supplies the durable developer-facing contract that minimal handlers do
/// not infer on their own.
/// </summary>
public sealed class MiniErpOpenApiDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "Mini ERP SaaS Platform API",
            Version = context.DocumentName,
            Description = "Generated first-party REST contract for the reusable B2B ERP platform. "
                + "Requests are resolved against server-derived authentication, Tenant ownership, "
                + "permission, scope, correlation, antiforgery, idempotency, and optimistic-concurrency "
                + "facts. Mutation evidence is mandatory where the operation catalogue says so. "
                + "Master Data Tax/VAT behavior is internal configuration-led reference data and a "
                + "deterministic engine contract over an explicit taxable base and other explicit inputs; this API does not claim statutory "
                + "certification, government submission, external provider connectivity, Finance posting, "
                + "or posted-document correction behavior."
        };

        return Task.CompletedTask;
    }
}

/// <summary>
/// Documents every mapped public operation from the immutable Foundation
/// operation catalogue. This avoids undocumented minimal-API handlers while
/// preserving the catalogue as the source of permission and boundary truth.
/// </summary>
public sealed class MiniErpOpenApiOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<FoundationOperationMetadata>()
            .SingleOrDefault();
        if (metadata is null || metadata.Visibility != FoundationOperationVisibility.Public)
        {
            return Task.CompletedTask;
        }

        var descriptor = metadata.Descriptor;
        operation.OperationId = descriptor.OperationId;
        operation.Summary = SummaryFor(descriptor.OperationId);
        operation.Description = DescriptionFor(descriptor);
        operation.Responses ??= new OpenApiResponses();
        AddResponse(operation, "200", SuccessResponseFor(descriptor.OperationId));
        AddResponse(operation, "400", "The request shape, effective date, explicit calculation input, or validation rule is invalid.");
        AddResponse(operation, "401", "The caller has no authenticated first-party session for this operation.");
        AddResponse(operation, "403", "The server-derived Tenant, permission, scope, or antiforgery authority does not allow the operation.");
        AddResponse(operation, "404", "The Tenant-owned resource or effective-dated version is not available.");
        AddResponse(operation, "409", "The request conflicts with an existing identity, effective window, idempotency binding, or optimistic-concurrency version.");
        AddResponse(operation, "503", "The required persistence, authorization, or audit boundary is unavailable; no success is claimed.");

        return Task.CompletedTask;
    }

    private static void AddResponse(OpenApiOperation operation, string statusCode, string description)
    {
        if (!operation.Responses!.ContainsKey(statusCode))
        {
            operation.Responses[statusCode] = new OpenApiResponse { Description = description };
        }
    }

    private static string SummaryFor(string operationId) => operationId switch
    {
        "platform.health" => "Check platform availability",
        "platform.openapi" => "Read the generated API contract",
        "platform.module-registration" => "Read registered module boundaries",
        "master-data.tax.list" => "List Tenant-owned Tax rules",
        "master-data.tax.read" => "Read one Tenant-owned Tax rule",
        "master-data.tax.history.read" => "Read Tax rate-version history",
        "master-data.tax.reference.read" => "Resolve a Tax version for an effective date",
        "master-data.tax.calculate" => "Calculate Tax from explicit engine inputs",
        "master-data.tax.create" => "Create a Tenant-owned Tax rule",
        "master-data.tax.edit" => "Edit Tax identity and append a rate version",
        "master-data.tax.deactivate" => "Deactivate a Tax rule",
        "master-data.tax.reactivate" => "Reactivate a Tax rule",
        "master-data.tax.audit.read" => "Read Tax audit evidence",
        _ => GenericSummary(operationId)
    };

    private static string GenericSummary(string operationId)
    {
        var parts = operationId.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return "Use the documented platform operation";
        var resource = ToDisplay(parts[^2]);
        var action = parts[^1] switch
        {
            "list" => "List",
            "read" => "Read",
            "create" => "Create",
            "edit" => "Edit",
            "deactivate" => "Deactivate",
            "reactivate" => "Reactivate",
            "audit" => "Read audit evidence",
            "preview" => "Preview",
            _ => "Use"
        };
        return $"{action} {resource}";
    }

    private static string DescriptionFor(FoundationOperationDescriptor descriptor)
    {
        var contextRules = $"Security profile: {descriptor.SecurityProfile}. Scope policy: {descriptor.ScopePolicy}. "
            + $"Exact permission: {descriptor.ExactPermissionCode ?? "none"}. "
            + $"Antiforgery required: {descriptor.RequiresAntiforgery}. "
            + $"Mandatory audit evidence: {descriptor.RequiresMandatoryAudit}. "
            + "Tenant and actor authority are derived by the server; request fields cannot select a foreign Tenant or broaden scope. ";

        if (descriptor.OperationId.StartsWith("master-data.tax", StringComparison.Ordinal))
        {
            return contextRules
                + "Tax is reusable Tenant-wide internal configuration-led Master Data with bilingual identity, "
                + "Active/Inactive lifecycle, effective-dated rate versions, and historical reference snapshots. "
                + "The calculation operation accepts an explicit taxable base, currency, rounding scale/mode, "
                + "transaction direction, effective date, and source lineage. It performs no accounting posting "
                + "and does not invent inclusive/exclusive price derivation, discount/charge/freight base policy, "
                + "exemption policy, statutory meaning, government submission, external provider behavior, or "
                + "posted-document correction. Writes require Idempotency-Key; identity/version edits and lifecycle "
                + "changes also require the current If-Match value."
                + (descriptor.OperationId == "master-data.tax.calculate"
                    ? " A successful calculation is side-effect-free and is not a mutation; a denied or invalid attempt may still append denial evidence."
                    : string.Empty);
        }

        return contextRules
            + "The operation is part of the reusable internal ERP contract. Response failures use Problem Details "
            + "with a stable code, correlation identifier, and operation identifier; provider details and internal "
            + "implementation types are not exposed."
            + (descriptor.IsUnsafe
                ? " The operation is a state-changing boundary and must preserve idempotency, concurrency, and audit rules."
                : string.Empty);
    }

    private static string SuccessResponseFor(string operationId) => operationId switch
    {
        "master-data.tax.calculate" => "A deterministic Tax amount and immutable reference snapshot for the explicit inputs.",
        "master-data.tax.reference.read" => "The active Tax rate version selected for the requested effective date, including applied reference evidence.",
        "master-data.tax.history.read" => "The Tenant-owned Tax rate-version windows in stable version order.",
        "master-data.tax.audit.read" => "Tenant-filtered audit evidence for the Tax resource.",
        _ => "The documented operation result with no provider or internal implementation details."
    };

    private static string ToDisplay(string value) =>
        string.Join(' ', value.Split('-', StringSplitOptions.RemoveEmptyEntries).Select(item =>
            item.Length == 0 ? item : char.ToUpperInvariant(item[0]) + item[1..]));
}

#pragma warning restore CS1591
