using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Procurement
{
    /// <inheritdoc />
    public partial class InitialProcurement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "procurement");

            migrationBuilder.CreateTable(
                name: "PurchaseRequests",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequesterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovalPolicySnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32768, nullable: false),
                    CurrentApprovalStageIndex = table.Column<int>(type: "int", nullable: false),
                    CurrentStageApprovalCount = table.Column<int>(type: "int", nullable: false),
                    CurrentStageApproverIdsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8192, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRequests", x => x.Id);
                    table.UniqueConstraint("AK_PurchaseRequests_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "PurchaseRequestAudit",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OperationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorizationPath = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    BeforeStatus = table.Column<int>(type: "int", nullable: true),
                    AfterStatus = table.Column<int>(type: "int", nullable: true),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BeforeSummary = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    AfterSummary = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRequestAudit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseRequestAudit_PurchaseRequests_TenantId_PurchaseRequestId",
                        columns: x => new { x.TenantId, x.PurchaseRequestId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseRequests",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseRequestHistory",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: false),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
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
                    table.PrimaryKey("PK_PurchaseRequestHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseRequestHistory_PurchaseRequests_TenantId_PurchaseRequestId",
                        columns: x => new { x.TenantId, x.PurchaseRequestId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseRequests",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseRequestLines",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductSku = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitOfMeasureCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    NeedByDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRequestLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseRequestLines_PurchaseRequests_TenantId_PurchaseRequestId",
                        columns: x => new { x.TenantId, x.PurchaseRequestId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseRequests",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierQuotations",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SupplierName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SupplierQuotationReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OfferDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    CurrencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CurrencyName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PaymentTermId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PaymentTermCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PaymentTermName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PaymentTermVersion = table.Column<int>(type: "int", nullable: true),
                    DeliveryTerms = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    OfferedDeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OfferedDeliveryLeadTime = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierQuotations", x => x.Id);
                    table.UniqueConstraint("AK_SupplierQuotations_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_SupplierQuotations_PurchaseRequests_TenantId_PurchaseRequestId",
                        columns: x => new { x.TenantId, x.PurchaseRequestId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseRequests",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierQuotationAudit",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierQuotationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_SupplierQuotationAudit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierQuotationAudit_SupplierQuotations_TenantId_SupplierQuotationId",
                        columns: x => new { x.TenantId, x.SupplierQuotationId },
                        principalSchema: "procurement",
                        principalTable: "SupplierQuotations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierQuotationEvidence",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierQuotationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_SupplierQuotationEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierQuotationEvidence_SupplierQuotations_TenantId_SupplierQuotationId",
                        columns: x => new { x.TenantId, x.SupplierQuotationId },
                        principalSchema: "procurement",
                        principalTable: "SupplierQuotations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierQuotationHistory",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierQuotationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_SupplierQuotationHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierQuotationHistory_SupplierQuotations_TenantId_SupplierQuotationId",
                        columns: x => new { x.TenantId, x.SupplierQuotationId },
                        principalSchema: "procurement",
                        principalTable: "SupplierQuotations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierQuotationLines",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierQuotationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseRequestLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductSku = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitOfMeasureCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RequestedQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    QuotedQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    DiscountPercentage = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    TaxId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TaxCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TaxName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    TaxRatePercentage = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    TaxAmount = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    TaxReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RequestedNeedByDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OfferedDeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OfferedDeliveryLeadTime = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierQuotationLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierQuotationLines_SupplierQuotations_TenantId_SupplierQuotationId",
                        columns: x => new { x.TenantId, x.SupplierQuotationId },
                        principalSchema: "procurement",
                        principalTable: "SupplierQuotations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierSourceDecisions",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SelectedQuotationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SupplierName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SupplierQuotationReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SelectedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Rationale = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: false),
                    PolicyId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PolicyVersion = table.Column<int>(type: "int", nullable: true),
                    StageKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ComparisonSnapshotReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ComparisonSnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 262144, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierSourceDecisions", x => x.Id);
                    table.UniqueConstraint("AK_SupplierSourceDecisions_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_SupplierSourceDecisions_PurchaseRequests_TenantId_PurchaseRequestId",
                        columns: x => new { x.TenantId, x.PurchaseRequestId },
                        principalSchema: "procurement",
                        principalTable: "PurchaseRequests",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierSourceDecisions_SupplierQuotations_TenantId_SelectedQuotationId",
                        columns: x => new { x.TenantId, x.SelectedQuotationId },
                        principalSchema: "procurement",
                        principalTable: "SupplierQuotations",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplierSourceDecisionHistory",
                schema: "procurement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousSelectedQuotationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SelectedQuotationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SelectedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Rationale = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: false),
                    PolicyId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PolicyVersion = table.Column<int>(type: "int", nullable: true),
                    StageKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ComparisonSnapshotReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ComparisonSnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 262144, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierSourceDecisionHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierSourceDecisionHistory_SupplierSourceDecisions_TenantId_SourceDecisionId",
                        columns: x => new { x.TenantId, x.SourceDecisionId },
                        principalSchema: "procurement",
                        principalTable: "SupplierSourceDecisions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestAudit_TenantId_Id",
                schema: "procurement",
                table: "PurchaseRequestAudit",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestAudit_TenantId_PurchaseRequestId_OccurredAt",
                schema: "procurement",
                table: "PurchaseRequestAudit",
                columns: new[] { "TenantId", "PurchaseRequestId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestHistory_TenantId_Id",
                schema: "procurement",
                table: "PurchaseRequestHistory",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestHistory_TenantId_PurchaseRequestId_OccurredAt",
                schema: "procurement",
                table: "PurchaseRequestHistory",
                columns: new[] { "TenantId", "PurchaseRequestId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestLines_TenantId_Id",
                schema: "procurement",
                table: "PurchaseRequestLines",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestLines_TenantId_PurchaseRequestId",
                schema: "procurement",
                table: "PurchaseRequestLines",
                columns: new[] { "TenantId", "PurchaseRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequests_TenantId_CompanyId_BranchId_CreatedAt",
                schema: "procurement",
                table: "PurchaseRequests",
                columns: new[] { "TenantId", "CompanyId", "BranchId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequests_TenantId_Id",
                schema: "procurement",
                table: "PurchaseRequests",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequests_TenantId_Status_UpdatedAt",
                schema: "procurement",
                table: "PurchaseRequests",
                columns: new[] { "TenantId", "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierQuotationAudit_TenantId_ActorId_OperationId_IdempotencyKey",
                schema: "procurement",
                table: "SupplierQuotationAudit",
                columns: new[] { "TenantId", "ActorId", "OperationId", "IdempotencyKey" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierQuotationAudit_TenantId_Id",
                schema: "procurement",
                table: "SupplierQuotationAudit",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierQuotationAudit_TenantId_SupplierQuotationId_OccurredAt",
                schema: "procurement",
                table: "SupplierQuotationAudit",
                columns: new[] { "TenantId", "SupplierQuotationId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierQuotationEvidence_TenantId_Id",
                schema: "procurement",
                table: "SupplierQuotationEvidence",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierQuotationEvidence_TenantId_SupplierQuotationId_RecordedAt",
                schema: "procurement",
                table: "SupplierQuotationEvidence",
                columns: new[] { "TenantId", "SupplierQuotationId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierQuotationHistory_TenantId_Id",
                schema: "procurement",
                table: "SupplierQuotationHistory",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierQuotationHistory_TenantId_SupplierQuotationId_OccurredAt",
                schema: "procurement",
                table: "SupplierQuotationHistory",
                columns: new[] { "TenantId", "SupplierQuotationId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierQuotationLines_TenantId_Id",
                schema: "procurement",
                table: "SupplierQuotationLines",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierQuotationLines_TenantId_PurchaseRequestId_PurchaseRequestLineId",
                schema: "procurement",
                table: "SupplierQuotationLines",
                columns: new[] { "TenantId", "PurchaseRequestId", "PurchaseRequestLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierQuotationLines_TenantId_SupplierQuotationId",
                schema: "procurement",
                table: "SupplierQuotationLines",
                columns: new[] { "TenantId", "SupplierQuotationId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierQuotations_TenantId_Id",
                schema: "procurement",
                table: "SupplierQuotations",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierQuotations_TenantId_PurchaseRequestId_CreatedAt",
                schema: "procurement",
                table: "SupplierQuotations",
                columns: new[] { "TenantId", "PurchaseRequestId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierQuotations_TenantId_Status_UpdatedAt",
                schema: "procurement",
                table: "SupplierQuotations",
                columns: new[] { "TenantId", "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierSourceDecisionHistory_TenantId_Id",
                schema: "procurement",
                table: "SupplierSourceDecisionHistory",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierSourceDecisionHistory_TenantId_PurchaseRequestId_SelectedAt",
                schema: "procurement",
                table: "SupplierSourceDecisionHistory",
                columns: new[] { "TenantId", "PurchaseRequestId", "SelectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierSourceDecisionHistory_TenantId_SourceDecisionId",
                schema: "procurement",
                table: "SupplierSourceDecisionHistory",
                columns: new[] { "TenantId", "SourceDecisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierSourceDecisions_TenantId_Id",
                schema: "procurement",
                table: "SupplierSourceDecisions",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierSourceDecisions_TenantId_PurchaseRequestId",
                schema: "procurement",
                table: "SupplierSourceDecisions",
                columns: new[] { "TenantId", "PurchaseRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierSourceDecisions_TenantId_SelectedQuotationId",
                schema: "procurement",
                table: "SupplierSourceDecisions",
                columns: new[] { "TenantId", "SelectedQuotationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseRequestAudit",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "PurchaseRequestHistory",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "PurchaseRequestLines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "SupplierQuotationAudit",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "SupplierQuotationEvidence",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "SupplierQuotationHistory",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "SupplierQuotationLines",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "SupplierSourceDecisionHistory",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "SupplierSourceDecisions",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "SupplierQuotations",
                schema: "procurement");

            migrationBuilder.DropTable(
                name: "PurchaseRequests",
                schema: "procurement");
        }
    }
}
