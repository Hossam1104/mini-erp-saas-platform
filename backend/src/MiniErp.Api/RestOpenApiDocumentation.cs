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
        AddExchangeRateReferenceParameter(operation, descriptor);
        AddPriceListReferenceParameters(operation, descriptor);
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

    private static void AddExchangeRateReferenceParameter(OpenApiOperation operation, FoundationOperationDescriptor descriptor)
    {
        if (descriptor.OperationId != "master-data.exchange-rate.reference.read")
        {
            return;
        }

        operation.Parameters ??= [];
        var parameter = operation.Parameters
            .OfType<OpenApiParameter>()
            .SingleOrDefault(item => item.Name == "effectiveOn" && item.In == ParameterLocation.Query);

        if (parameter is null)
        {
            parameter = new OpenApiParameter
            {
                Name = "effectiveOn",
                In = ParameterLocation.Query,
                Required = true
            };
            operation.Parameters.Add(parameter);
        }

        parameter.Description = "Required ISO 8601 calendar date used to select exactly one effective-dated version.";
        parameter.Required = true;
        parameter.Schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Format = "date"
        };
    }

    private static void AddPriceListReferenceParameters(OpenApiOperation operation, FoundationOperationDescriptor descriptor)
    {
        if (descriptor.OperationId != "master-data.price-list.reference.read")
        {
            return;
        }

        operation.Parameters ??= [];
        AddQueryParameter(operation, "priceListId", "Optional Tenant-owned Price List identity used to constrain resolution.", JsonSchemaType.String, "uuid", false);
        AddQueryParameter(operation, "productId", "Required existing active Product identity.", JsonSchemaType.String, "uuid", true);
        AddQueryParameter(operation, "unitOfMeasureId", "Required existing active UOM identity; no implicit conversion is applied.", JsonSchemaType.String, "uuid", true);
        AddQueryParameter(operation, "currencyId", "Required existing active Currency identity; the selected Price List must match exactly.", JsonSchemaType.String, "uuid", true);
        AddQueryParameter(operation, "customerId", "Optional same-Tenant Business Customer applicability input.", JsonSchemaType.String, "uuid", false);
        AddQueryParameter(operation, "organizationScopeKind", "Optional Company or Branch applicability kind.", JsonSchemaType.String, null, false);
        AddQueryParameter(operation, "organizationScopeId", "Optional server-authorized Company or Branch applicability identity.", JsonSchemaType.String, "uuid", false);
        AddQueryParameter(operation, "effectiveOn", "Required ISO 8601 calendar date used to select exactly one effective-dated price.", JsonSchemaType.String, "date", true);
    }

    private static void AddQueryParameter(OpenApiOperation operation, string name, string description, JsonSchemaType type, string? format, bool required)
    {
        operation.Parameters ??= [];
        var parameter = operation.Parameters
            .OfType<OpenApiParameter>()
            .SingleOrDefault(item => item.Name == name && item.In == ParameterLocation.Query);
        if (parameter is null)
        {
            parameter = new OpenApiParameter { Name = name, In = ParameterLocation.Query };
            operation.Parameters.Add(parameter);
        }

        parameter.Description = description;
        parameter.Required = required;
        parameter.Schema = new OpenApiSchema { Type = type, Format = format };
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
        "master-data.exchange-rate.list" => "List Tenant-owned Exchange Rates",
        "master-data.exchange-rate.read" => "Read one Tenant-owned Exchange Rate",
        "master-data.exchange-rate.history.read" => "Read Exchange Rate version history",
        "master-data.exchange-rate.reference.read" => "Resolve an Exchange Rate for an effective date",
        "master-data.exchange-rate.create" => "Create a Tenant-owned Exchange Rate",
        "master-data.exchange-rate.edit" => "Edit an Exchange Rate and append evidence",
        "master-data.exchange-rate.deactivate" => "Deactivate an Exchange Rate",
        "master-data.exchange-rate.reactivate" => "Reactivate an Exchange Rate",
        "master-data.exchange-rate.audit.read" => "Read Exchange Rate audit evidence",
        "master-data.price-list.list" => "List Tenant-owned Price Lists",
        "master-data.price-list.read" => "Read one Tenant-owned Price List",
        "master-data.price-list.history.read" => "Read Price List price-version history",
        "master-data.price-list.create" => "Create a Tenant-owned Price List",
        "master-data.price-list.edit" => "Edit Price List identity and applicability",
        "master-data.price-list.price.append" => "Append an effective-dated Price List price",
        "master-data.price-list.deactivate" => "Deactivate a Price List",
        "master-data.price-list.reactivate" => "Reactivate a Price List",
        "master-data.price-list.reference.read" => "Resolve a deterministic B2B price reference",
        "master-data.price-list.audit.read" => "Read Price List audit evidence",
        "master-data.import.create" => "Create a Tenant-owned Master Data import batch",
        "master-data.import.simulate" => "Validate a Master Data import without mutations",
        "master-data.import.execute" => "Execute a validated Master Data import batch",
        "master-data.import.list" => "List Tenant-owned Master Data import batches",
        "master-data.import.read" => "Read one Tenant-owned Master Data import batch",
        "master-data.import.status.read" => "Read Master Data import status",
        "master-data.import.rows.read" => "Read Master Data import row evidence",
        "master-data.import.reconciliation.read" => "Read Master Data import reconciliation",
        "master-data.import.audit.read" => "Read Master Data import audit evidence",
        "master-data.import.evidence.read" => "Read complete Master Data import evidence",
        "master-data.import.replay" => "Replay one quarantined Master Data import row",
        "procurement.organization-scope.list" => "List server-authorized Purchase Request organization scopes",
        "procurement.purchase-request.list" => "List Tenant-scoped Purchase Requests",
        "procurement.purchase-request.read" => "Read one Purchase Request",
        "procurement.purchase-request.create" => "Create a Purchase Request draft",
        "procurement.purchase-request.edit" => "Edit a Purchase Request draft",
        "procurement.purchase-request.submit" => "Submit a Purchase Request for approval",
        "procurement.purchase-request.approve" => "Approve a Purchase Request",
        "procurement.purchase-request.reject" => "Reject a Purchase Request",
        "procurement.purchase-request.return-for-change" => "Return a Purchase Request for change",
        "procurement.purchase-request.cancel" => "Cancel an eligible Purchase Request",
        "procurement.purchase-request.history.read" => "Read Purchase Request lifecycle history",
        "procurement.purchase-request.audit.read" => "Read Purchase Request audit evidence",
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
            + $"Concurrency contract: {descriptor.Concurrency}. "
            + $"Idempotency contract: {descriptor.Idempotency}. "
            + $"Effective-date contract: {descriptor.EffectiveDate}. "
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

        if (descriptor.OperationId.StartsWith("master-data.exchange-rate", StringComparison.Ordinal))
        {
            return contextRules
                + "Exchange Rate is reusable Tenant-wide internal reference data over two existing active Currency identities. "
                + "The pair is directional (source units to target units), the rate is positive with an explicit precision scale, "
                + "and edits append effective-dated versions while preserving manual/configured provenance, source notes, and "
                + "historical Currency-code snapshots. Reference resolution selects one version only when the requested date is "
                + "inside its effective window; unknown or inactive references fail safely. This API does not invent inverse, "
                + "reciprocal, triangulated, provider-fed, bid/ask, average, daily-close, Finance posting, rounding, realized or "
                + "unrealized FX, revaluation, settlement, reconciliation, or external integration behavior. Writes require "
                + "Idempotency-Key; version edits and lifecycle changes also require the current If-Match value.";
        }

        if (descriptor.OperationId.StartsWith("master-data.price-list", StringComparison.Ordinal))
        {
            return contextRules
                + "Price Lists are reusable Tenant-owned B2B configuration over existing Product, UOM, Currency, and optional same-Tenant Business Customer identities. "
                + "Each price carries an effective window, provenance, source reference, and immutable Product/UOM/Currency applicability evidence. "
                + "Reference resolution requires exact Product, UOM, Currency, customer applicability, organization applicability, lifecycle, and effective-date matches. "
                + "The configured priority uses lower numeric values as higher precedence; equal best priority is an explicit conflict and never falls back to edit order, identifier order, or UI order. "
                + "The endpoint does not implement quantity breaks, promotions, coupons, campaigns, POS, Sales Orders, manual override bypasses, Finance, automatic FX, or accounting rounding. "
                + "Writes require Idempotency-Key, and Price List identity/price/lifecycle writes require the current If-Match value where the catalogue declares concurrency.";
        }

        if (descriptor.OperationId.StartsWith("master-data.import", StringComparison.Ordinal))
        {
            return contextRules
                + "This Phase-A import boundary is a structured, Tenant-owned, evidence-first workflow for Category, UOM, Product, Supplier, "
                + "Business Customer, Currency, Payment Term, Tax/VAT, Exchange Rate, and Price List resources. Each batch preserves source "
                + "provenance, original payload/row identity, normalized fields, diagnostics, outcome, mutation disposition, deterministic result "
                + "references, replay lineage, and audit evidence. Simulation validates and resolves references without calling target mutation "
                + "adapters; execution is permitted only for a validated Commit-mode batch and applies the configured Reject, SkipExisting, or "
                + "UpdateMutableFields duplicate policy. Server-derived Tenant and actor authority cannot be supplied by row data. Partial success "
                + "is reconciled as TotalRows = Accepted + Rejected + Quarantined, with committed, skipped, and failed mutation counts shown "
                + "separately. Replay creates a new current attempt while preserving the original quarantined evidence and lineage. This boundary "
                + "does not perform MESP-40 migration/cutover, retention or legal-hold behavior, residency/PDPL certification, provider integration, "
                + "or irreversible production migration decisions.";
        }

        if (descriptor.OperationId == "procurement.organization-scope.list")
        {
            return contextRules
                + "Organization scopes are the trusted, server-configured set of Company/Branch options a caller may select when creating "
                + "or editing a Purchase Request. Options are filtered to the caller's Tenant and further narrowed by the caller's own "
                + "trusted authorization scope; a request can never widen this to an arbitrary Company or Branch identity. This is not a "
                + "Company/Branch CRUD boundary; it exposes read-only display names for an existing or Development-configured organization "
                + "structure so the client never needs to type or display a raw internal identifier as the primary means of selection.";
        }

        if (descriptor.OperationId.StartsWith("procurement.purchase-request", StringComparison.Ordinal))
        {
            return contextRules
                + "Purchase Request is an internal Tenant/company/branch demand signal containing Product, UOM, quantity, need-by date, and purpose lines. "
                + "The lifecycle is Draft, PendingApproval, Approved, Rejected, ReturnedForChange, or Cancelled. Submission freezes the reviewed request version; "
                + "approval is configuration-led and records immutable history, including bounded delegation evidence where configured. Self-approval is denied, "
                + "missing or expired authority blocks the decision, and cancellation is available only in eligible states. This boundary creates no stock, supplier "
                + "commitment, Purchase Order, receipt, invoice, AP, payment, or accounting effect. Mutations require Idempotency-Key, the current If-Match value where "
                + "declared, antiforgery, and mandatory audit evidence.";
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
        "master-data.exchange-rate.reference.read" => "The active Exchange Rate version selected for the requested effective date, including applied pair and version evidence.",
        "master-data.exchange-rate.history.read" => "The Tenant-owned Exchange Rate version windows with preserved Currency-code snapshots.",
        "master-data.exchange-rate.audit.read" => "Tenant-filtered audit evidence for the Exchange Rate resource.",
        "master-data.price-list.reference.read" => "The single deterministic Tenant-owned Price List price reference, including Product/UOM/Currency applicability, effective window, configured priority, provenance, and immutable version evidence.",
        "master-data.price-list.history.read" => "The Tenant-owned Price List price-version history with preserved applicability and provenance snapshots.",
        "master-data.price-list.audit.read" => "Tenant-filtered audit evidence for the Price List resource and pricing-reference decisions.",
        "master-data.import.create" => "The persisted import batch identity, source provenance, policy, mode, initial status, version, and reconciliation counters.",
        "master-data.import.simulate" => "The validated import batch with row-level normalized evidence, diagnostics, duplicate/reference outcomes, and no target mutations.",
        "master-data.import.execute" => "The completed or partially completed import batch with row outcomes, mutation dispositions, deterministic target references, and reconciliation counters.",
        "master-data.import.list" => "Tenant-filtered import batch summaries with status, provenance, mode, policy, version, and reconciliation counters.",
        "master-data.import.read" => "One Tenant-filtered import batch summary with its durable lifecycle and reconciliation state.",
        "master-data.import.status.read" => "The current import status, correlation identifier, and optimistic-concurrency version.",
        "master-data.import.rows.read" => "Tenant-filtered row evidence including source fields, normalized fields, diagnostics, outcome, mutation disposition, target reference, and replay lineage.",
        "master-data.import.reconciliation.read" => "The persisted import reconciliation counters and the TotalRows = Accepted + Rejected + Quarantined consistency result.",
        "master-data.import.audit.read" => "Tenant-filtered batch, row, and mutation audit evidence with source reference and correlation context.",
        "master-data.import.evidence.read" => "The complete Tenant-filtered import batch, row, reconciliation, and audit evidence package.",
        "master-data.import.replay" => "The updated import batch after replaying one quarantined row, preserving the original evidence and adding a new current attempt.",
        "procurement.organization-scope.list" => "The Tenant- and scope-filtered set of server-authorized Company/Branch options with human-readable display names.",
        "procurement.purchase-request.list" => "Tenant-filtered Purchase Request summaries with scope, status, line count, and concurrency version.",
        "procurement.purchase-request.read" => "One Tenant-filtered Purchase Request with its lines, approval snapshot, lifecycle state, and server-derived action affordances.",
        "procurement.purchase-request.create" => "The persisted Draft Purchase Request and its Product/UOM line snapshots.",
        "procurement.purchase-request.edit" => "The updated Draft or ReturnedForChange Purchase Request with a new optimistic-concurrency version.",
        "procurement.purchase-request.submit" => "The Purchase Request in PendingApproval with the effective approval-policy snapshot.",
        "procurement.purchase-request.approve" => "The Purchase Request after the immutable approval decision and any resulting stage transition.",
        "procurement.purchase-request.reject" => "The rejected Purchase Request with the recorded reason and approval evidence.",
        "procurement.purchase-request.return-for-change" => "The Purchase Request returned for change with the recorded reason and approval evidence.",
        "procurement.purchase-request.cancel" => "The eligible Purchase Request in Cancelled status with immutable cancellation evidence.",
        "procurement.purchase-request.history.read" => "Immutable Tenant-filtered lifecycle and approval history for the Purchase Request.",
        "procurement.purchase-request.audit.read" => "Immutable Tenant-filtered operation audit evidence for the Purchase Request.",
        _ => "The documented operation result with no provider or internal implementation details."
    };

    private static string ToDisplay(string value) =>
        string.Join(' ', value.Split('-', StringSplitOptions.RemoveEmptyEntries).Select(item =>
            item.Length == 0 ? item : char.ToUpperInvariant(item[0]) + item[1..]));
}

#pragma warning restore CS1591
