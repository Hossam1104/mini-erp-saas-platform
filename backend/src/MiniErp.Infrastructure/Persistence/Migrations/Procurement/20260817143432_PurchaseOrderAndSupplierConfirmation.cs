using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Procurement
{
    /// <inheritdoc />
    public partial class PurchaseOrderAndSupplierConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierQuotationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SupplierName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SupplierQuotationReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CurrencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CurrencyName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PaymentTermId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PaymentTermCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PaymentTermName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PaymentTermVersion = table.Column<int>(type: "int", nullable: true),
                    SourcePurchaseRequestReference = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourcePurchaseRequestPurpose = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    SourceDecisionRationale = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: false),
                    SourceSelectedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IssuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LatestConfirmationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LatestConfirmationStatus = table.Column<int>(type: "int", nullable: true),
                    StatusBeforeSupplierChange = table.Column<int>(type: "int", nullable: true),
                    ApprovalPolicySnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32768, nullable: false),
                    CurrentApprovalStageIndex = table.Column<int>(type: "int", nullable: false),
                    CurrentStageApprovalCount = table.Column<int>(type: "int", nullable: false),
                    CurrentStageApproverIdsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8192, nullable: false),
                    IsReapproval = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrders", x => x.Id);
                    table.UniqueConstraint("AK_PurchaseOrders_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_PurchaseRequests_TenantId_PurchaseRequestId",
                        columns: x => new { x.TenantId, x.PurchaseRequestId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseRequests",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_SupplierQuotations_TenantId_SupplierQuotationId",
                        columns: x => new { x.TenantId, x.SupplierQuotationId },
                        principalSchema: "procurement",
                        principalTable: "SupplierQuotations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_SupplierSourceDecisions_TenantId_SourceDecisionId",
                        columns: x => new { x.TenantId, x.SourceDecisionId },
                        principalSchema: "procurement",
                        principalTable: "SupplierSourceDecisions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderAudit",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OperationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorizationPath = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    BeforeStatus = table.Column<int>(type: "int", nullable: true),
                    AfterStatus = table.Column<int>(type: "int", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BeforeSummary = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    AfterSummary = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderAudit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderAudit_PurchaseOrders_TenantId_PurchaseOrderId",
                        columns: x => new { x.TenantId, x.PurchaseOrderId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseOrders",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderConfirmations",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ResponseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SupplierReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SupplierContact = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    RecordedByActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PurchaseOrderVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderConfirmations", x => x.Id);
                    table.UniqueConstraint("AK_PurchaseOrderConfirmations_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_PurchaseOrderConfirmations_PurchaseOrders_TenantId_PurchaseOrderId",
                        columns: x => new { x.TenantId, x.PurchaseOrderId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseOrders",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderHistory",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: false),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PolicyId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PolicyVersion = table.Column<int>(type: "int", nullable: true),
                    StageKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DelegatedFromActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderHistory_PurchaseOrders_TenantId_PurchaseOrderId",
                        columns: x => new { x.TenantId, x.PurchaseOrderId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseOrders",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderLines",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceQuotationLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseRequestLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductSku = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitOfMeasureCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RequestedQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    OrderedQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    ConfirmedQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    DiscountPercentage = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    TaxId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TaxCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TaxName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    TaxRatePercentage = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    TaxAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    RequestedNeedByDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderLines", x => x.Id);
                    table.UniqueConstraint("AK_PurchaseOrderLines_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLines_PurchaseOrders_TenantId_PurchaseOrderId",
                        columns: x => new { x.TenantId, x.PurchaseOrderId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseOrders",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderEvidence",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderConfirmationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ExternalReference = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    RecordedByActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderEvidence_PurchaseOrderConfirmations_TenantId_PurchaseOrderConfirmationId",
                        columns: x => new { x.TenantId, x.PurchaseOrderConfirmationId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseOrderConfirmations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderConfirmationLines",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderConfirmationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderedQuantityAtResponse = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    ConfirmedQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    ExpectedDeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ProposedQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    ProposedUnitPrice = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    ProposedDeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ChangeReason = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderConfirmationLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderConfirmationLines_PurchaseOrderConfirmations_TenantId_PurchaseOrderConfirmationId",
                        columns: x => new { x.TenantId, x.PurchaseOrderConfirmationId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseOrderConfirmations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderConfirmationLines_PurchaseOrderLines_TenantId_PurchaseOrderLineId",
                        columns: x => new { x.TenantId, x.PurchaseOrderLineId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseOrderLines",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderSupplierChanges",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderConfirmationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousOrderedQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    ProposedQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    PreviousUnitPrice = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    ProposedUnitPrice = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    PreviousDeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ProposedDeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProposedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderSupplierChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderSupplierChanges_PurchaseOrderConfirmations_TenantId_PurchaseOrderConfirmationId",
                        columns: x => new { x.TenantId, x.PurchaseOrderConfirmationId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseOrderConfirmations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderSupplierChanges_PurchaseOrderLines_TenantId_PurchaseOrderLineId",
                        columns: x => new { x.TenantId, x.PurchaseOrderLineId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseOrderLines",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderAudit_TenantId_ActorId_OperationId_IdempotencyKey",
                schema: "procurement",
                table: "PurchaseOrderAudit",
                columns: new[] { "TenantId", "ActorId", "OperationId", "IdempotencyKey" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderAudit_TenantId_Id",
                schema: "procurement",
                table: "PurchaseOrderAudit",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderAudit_TenantId_PurchaseOrderId_OccurredAt",
                schema: "procurement",
                table: "PurchaseOrderAudit",
                columns: new[] { "TenantId", "PurchaseOrderId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderConfirmationLines_TenantId_Id",
                schema: "procurement",
                table: "PurchaseOrderConfirmationLines",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderConfirmationLines_TenantId_PurchaseOrderConfirmationId",
                schema: "procurement",
                table: "PurchaseOrderConfirmationLines",
                columns: new[] { "TenantId", "PurchaseOrderConfirmationId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderConfirmationLines_TenantId_PurchaseOrderLineId",
                schema: "procurement",
                table: "PurchaseOrderConfirmationLines",
                columns: new[] { "TenantId", "PurchaseOrderLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderConfirmations_TenantId_Id",
                schema: "procurement",
                table: "PurchaseOrderConfirmations",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderConfirmations_TenantId_PurchaseOrderId_RecordedAt",
                schema: "procurement",
                table: "PurchaseOrderConfirmations",
                columns: new[] { "TenantId", "PurchaseOrderId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderEvidence_TenantId_Id",
                schema: "procurement",
                table: "PurchaseOrderEvidence",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderEvidence_TenantId_PurchaseOrderConfirmationId_RecordedAt",
                schema: "procurement",
                table: "PurchaseOrderEvidence",
                columns: new[] { "TenantId", "PurchaseOrderConfirmationId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderHistory_TenantId_Id",
                schema: "procurement",
                table: "PurchaseOrderHistory",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderHistory_TenantId_PurchaseOrderId_OccurredAt",
                schema: "procurement",
                table: "PurchaseOrderHistory",
                columns: new[] { "TenantId", "PurchaseOrderId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_TenantId_Id",
                schema: "procurement",
                table: "PurchaseOrderLines",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_TenantId_PurchaseOrderId",
                schema: "procurement",
                table: "PurchaseOrderLines",
                columns: new[] { "TenantId", "PurchaseOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_TenantId_Id",
                schema: "procurement",
                table: "PurchaseOrders",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_TenantId_PurchaseRequestId_CreatedAt",
                schema: "procurement",
                table: "PurchaseOrders",
                columns: new[] { "TenantId", "PurchaseRequestId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_TenantId_SourceDecisionId",
                schema: "procurement",
                table: "PurchaseOrders",
                columns: new[] { "TenantId", "SourceDecisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_TenantId_Status_UpdatedAt",
                schema: "procurement",
                table: "PurchaseOrders",
                columns: new[] { "TenantId", "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_TenantId_SupplierQuotationId",
                schema: "procurement",
                table: "PurchaseOrders",
                columns: new[] { "TenantId", "SupplierQuotationId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderSupplierChanges_TenantId_Id",
                schema: "procurement",
                table: "PurchaseOrderSupplierChanges",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderSupplierChanges_TenantId_PurchaseOrderConfirmationId",
                schema: "procurement",
                table: "PurchaseOrderSupplierChanges",
                columns: new[] { "TenantId", "PurchaseOrderConfirmationId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderSupplierChanges_TenantId_PurchaseOrderLineId_Status",
                schema: "procurement",
                table: "PurchaseOrderSupplierChanges",
                columns: new[] { "TenantId", "PurchaseOrderLineId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseOrderAudit",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "PurchaseOrderConfirmationLines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "PurchaseOrderEvidence",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "PurchaseOrderHistory",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "PurchaseOrderSupplierChanges",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "PurchaseOrderConfirmations",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "PurchaseOrderLines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "PurchaseOrders",
                schema: "procurement");
        }
    }
}
