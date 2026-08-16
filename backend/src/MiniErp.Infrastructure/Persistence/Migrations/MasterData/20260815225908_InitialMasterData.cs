using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.MasterData
{
    /// <inheritdoc />
    public partial class InitialMasterData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "masterdata");

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                schema: "masterdata",
                columns: table => new
                {
                    EvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OperationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorizationPath = table.Column<int>(type: "int", nullable: false),
                    ResourceKind = table.Column<int>(type: "int", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Operation = table.Column<int>(type: "int", nullable: false),
                    PolicyOutcome = table.Column<int>(type: "int", nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<int>(type: "int", nullable: false),
                    BeforeSummary = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    AfterSummary = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ApproverId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScopePolicyId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ScopePolicyVersion = table.Column<int>(type: "int", nullable: false),
                    ScopeAnchorKind = table.Column<int>(type: "int", nullable: true),
                    ScopeAnchorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.EvidenceId);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                schema: "masterdata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EnglishName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ArabicName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NameKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ParentCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TrackingDefaultEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LifecycleState = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.UniqueConstraint("AK_Categories_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Categories_Categories_TenantId_ParentCategoryId",
                        columns: x => new { x.TenantId, x.ParentCategoryId },
                        principalSchema: "masterdata",
                        principalTable: "Categories",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Currencies",
                schema: "masterdata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CodeKey = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    EnglishName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ArabicName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NameKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LifecycleState = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Id);
                    table.UniqueConstraint("AK_Currencies_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "ImportAuditEvents",
                schema: "masterdata",
                columns: table => new
                {
                    EvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginalRowNumber = table.Column<int>(type: "int", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OperationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceKind = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportAuditEvents", x => x.EvidenceId);
                });

            migrationBuilder.CreateTable(
                name: "ImportBatches",
                schema: "masterdata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceKind = table.Column<int>(type: "int", nullable: false),
                    SourceSystemCategory = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourceFileReference = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    BatchReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DuplicatePolicy = table.Column<int>(type: "int", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubmittedActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TotalRows = table.Column<int>(type: "int", nullable: false),
                    AcceptedCount = table.Column<int>(type: "int", nullable: false),
                    RejectedCount = table.Column<int>(type: "int", nullable: false),
                    QuarantinedCount = table.Column<int>(type: "int", nullable: false),
                    CommittedCount = table.Column<int>(type: "int", nullable: false),
                    SkippedCount = table.Column<int>(type: "int", nullable: false),
                    FailedCount = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Fingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatches", x => x.Id);
                    table.UniqueConstraint("AK_ImportBatches_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "PaymentTerms",
                schema: "masterdata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CodeKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EnglishName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ArabicName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NameKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LifecycleState = table.Column<int>(type: "int", nullable: false),
                    CurrentVersionNumber = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTerms", x => x.Id);
                    table.UniqueConstraint("AK_PaymentTerms_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "Taxes",
                schema: "masterdata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CodeKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CategoryCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CategoryCodeKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CategoryEnglishName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CategoryArabicName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EnglishName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ArabicName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NameKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Applicability = table.Column<int>(type: "int", nullable: false),
                    LifecycleState = table.Column<int>(type: "int", nullable: false),
                    CurrentVersionNumber = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Taxes", x => x.Id);
                    table.UniqueConstraint("AK_Taxes_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "UnitsOfMeasure",
                schema: "masterdata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EnglishName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ArabicName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NameKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LifecycleState = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitsOfMeasure", x => x.Id);
                    table.UniqueConstraint("AK_UnitsOfMeasure_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "ExchangeRates",
                schema: "masterdata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceCurrencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetCurrencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LifecycleState = table.Column<int>(type: "int", nullable: false),
                    CurrentVersionNumber = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRates", x => x.Id);
                    table.UniqueConstraint("AK_ExchangeRates_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_ExchangeRates_Currencies_TenantId_SourceCurrencyId",
                        columns: x => new { x.TenantId, x.SourceCurrencyId },
                        principalSchema: "masterdata",
                        principalTable: "Currencies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExchangeRates_Currencies_TenantId_TargetCurrencyId",
                        columns: x => new { x.TenantId, x.TargetCurrencyId },
                        principalSchema: "masterdata",
                        principalTable: "Currencies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PriceLists",
                schema: "masterdata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CodeKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EnglishName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ArabicName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CurrencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrganizationScopeKind = table.Column<int>(type: "int", nullable: true),
                    OrganizationScopeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    LifecycleState = table.Column<int>(type: "int", nullable: false),
                    CurrentVersionNumber = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceLists", x => x.Id);
                    table.UniqueConstraint("AK_PriceLists_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_PriceLists_Currencies_TenantId_CurrencyId",
                        columns: x => new { x.TenantId, x.CurrencyId },
                        principalSchema: "masterdata",
                        principalTable: "Currencies",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ImportRows",
                schema: "masterdata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalRowNumber = table.Column<int>(type: "int", nullable: false),
                    ReplaySequence = table.Column<int>(type: "int", nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    ResourceKind = table.Column<int>(type: "int", nullable: false),
                    SourceFieldsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32768, nullable: false),
                    NormalizedFieldsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32768, nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    DiagnosticsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32768, nullable: false),
                    HighestSeverity = table.Column<int>(type: "int", nullable: false),
                    MutationDisposition = table.Column<int>(type: "int", nullable: false),
                    ResultingResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResultingResourceCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ExpectedResourceVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    ReplayOfRowId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginalRowId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReplayIdempotencyKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportRows_ImportBatches_TenantId_BatchId",
                        columns: x => new { x.TenantId, x.BatchId },
                        principalSchema: "masterdata",
                        principalTable: "ImportBatches",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentTermVersions",
                schema: "masterdata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentTermId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EnglishName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ArabicName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    BaseDateRule = table.Column<int>(type: "int", nullable: false),
                    ScheduleMode = table.Column<int>(type: "int", nullable: false),
                    DueOffsetDays = table.Column<int>(type: "int", nullable: false),
                    DueOffsetMonths = table.Column<int>(type: "int", nullable: false),
                    EarlySettlementDiscountEnabled = table.Column<bool>(type: "bit", nullable: false),
                    EarlySettlementDiscountPercentage = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    EarlySettlementDiscountDays = table.Column<int>(type: "int", nullable: false),
                    EarlySettlementDiscountMonths = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTermVersions", x => x.Id);
                    table.UniqueConstraint("AK_PaymentTermVersions_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_PaymentTermVersions_PaymentTerms_TenantId_PaymentTermId",
                        columns: x => new { x.TenantId, x.PaymentTermId },
                        principalSchema: "masterdata",
                        principalTable: "PaymentTerms",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaxRateVersions",
                schema: "masterdata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    RatePercentage = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxRateVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxRateVersions_Taxes_TenantId_TaxId",
                        columns: x => new { x.TenantId, x.TaxId },
                        principalSchema: "masterdata",
                        principalTable: "Taxes",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                schema: "masterdata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SkuKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EnglishName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ArabicName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BaseUnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrackingEnabledOverride = table.Column<bool>(type: "bit", nullable: true),
                    IsSellable = table.Column<bool>(type: "bit", nullable: false),
                    IsPurchasable = table.Column<bool>(type: "bit", nullable: false),
                    IsInventoryRelevant = table.Column<bool>(type: "bit", nullable: false),
                    LifecycleState = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.UniqueConstraint("AK_Products_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Products_Categories_TenantId_CategoryId",
                        columns: x => new { x.TenantId, x.CategoryId },
                        principalSchema: "masterdata",
                        principalTable: "Categories",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Products_UnitsOfMeasure_TenantId_BaseUnitOfMeasureId",
                        columns: x => new { x.TenantId, x.BaseUnitOfMeasureId },
                        principalSchema: "masterdata",
                        principalTable: "UnitsOfMeasure",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UnitConversions",
                schema: "masterdata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromUnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToUnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Factor = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitConversions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitConversions_UnitsOfMeasure_TenantId_FromUnitOfMeasureId",
                        columns: x => new { x.TenantId, x.FromUnitOfMeasureId },
                        principalSchema: "masterdata",
                        principalTable: "UnitsOfMeasure",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnitConversions_UnitsOfMeasure_TenantId_ToUnitOfMeasureId",
                        columns: x => new { x.TenantId, x.ToUnitOfMeasureId },
                        principalSchema: "masterdata",
                        principalTable: "UnitsOfMeasure",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeRateVersions",
                schema: "masterdata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExchangeRateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Rate = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    RateScale = table.Column<int>(type: "int", nullable: false),
                    Provenance = table.Column<int>(type: "int", nullable: false),
                    SourceNotes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    SourceCurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TargetCurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRateVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExchangeRateVersions_ExchangeRates_TenantId_ExchangeRateId",
                        columns: x => new { x.TenantId, x.ExchangeRateId },
                        principalSchema: "masterdata",
                        principalTable: "ExchangeRates",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentTermInstallments",
                schema: "masterdata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentTermVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Percentage = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    OffsetDays = table.Column<int>(type: "int", nullable: false),
                    OffsetMonths = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTermInstallments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentTermInstallments_PaymentTermVersions_TenantId_PaymentTermVersionId",
                        columns: x => new { x.TenantId, x.PaymentTermVersionId },
                        principalSchema: "masterdata",
                        principalTable: "PaymentTermVersions",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PriceListPrices",
                schema: "masterdata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PriceListId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductSku = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    UnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitOfMeasureCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CurrencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrganizationScopeKind = table.Column<int>(type: "int", nullable: true),
                    OrganizationScopeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Price = table.Column<decimal>(type: "decimal(28,12)", precision: 28, scale: 12, nullable: false),
                    PriceScale = table.Column<int>(type: "int", nullable: false),
                    Provenance = table.Column<int>(type: "int", nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceListPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceListPrices_PriceLists_TenantId_PriceListId",
                        columns: x => new { x.TenantId, x.PriceListId },
                        principalSchema: "masterdata",
                        principalTable: "PriceLists",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PriceListPrices_Products_TenantId_ProductId",
                        columns: x => new { x.TenantId, x.ProductId },
                        principalSchema: "masterdata",
                        principalTable: "Products",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PriceListPrices_UnitsOfMeasure_TenantId_UnitOfMeasureId",
                        columns: x => new { x.TenantId, x.UnitOfMeasureId },
                        principalSchema: "masterdata",
                        principalTable: "UnitsOfMeasure",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductBarcodes",
                schema: "masterdata",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ComparisonKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductBarcodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductBarcodes_Products_TenantId_ProductId",
                        columns: x => new { x.TenantId, x.ProductId },
                        principalSchema: "masterdata",
                        principalTable: "Products",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_TenantId_OccurredAt",
                schema: "masterdata",
                table: "AuditEvents",
                columns: new[] { "TenantId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_TenantId_ResourceKind_ResourceId",
                schema: "masterdata",
                table: "AuditEvents",
                columns: new[] { "TenantId", "ResourceKind", "ResourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_TenantId_Code",
                schema: "masterdata",
                table: "Categories",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_TenantId_Id",
                schema: "masterdata",
                table: "Categories",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_TenantId_NameKey",
                schema: "masterdata",
                table: "Categories",
                columns: new[] { "TenantId", "NameKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_TenantId_ParentCategoryId",
                schema: "masterdata",
                table: "Categories",
                columns: new[] { "TenantId", "ParentCategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_TenantId_CodeKey",
                schema: "masterdata",
                table: "Currencies",
                columns: new[] { "TenantId", "CodeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_TenantId_Id",
                schema: "masterdata",
                table: "Currencies",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_TenantId_NameKey",
                schema: "masterdata",
                table: "Currencies",
                columns: new[] { "TenantId", "NameKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_TenantId_Id",
                schema: "masterdata",
                table: "ExchangeRates",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_TenantId_SourceCurrencyId_TargetCurrencyId",
                schema: "masterdata",
                table: "ExchangeRates",
                columns: new[] { "TenantId", "SourceCurrencyId", "TargetCurrencyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_TenantId_TargetCurrencyId",
                schema: "masterdata",
                table: "ExchangeRates",
                columns: new[] { "TenantId", "TargetCurrencyId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRateVersions_TenantId_ExchangeRateId_EffectiveFrom",
                schema: "masterdata",
                table: "ExchangeRateVersions",
                columns: new[] { "TenantId", "ExchangeRateId", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRateVersions_TenantId_ExchangeRateId_VersionNumber",
                schema: "masterdata",
                table: "ExchangeRateVersions",
                columns: new[] { "TenantId", "ExchangeRateId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRateVersions_TenantId_Id",
                schema: "masterdata",
                table: "ExchangeRateVersions",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportAuditEvents_TenantId_BatchId_OccurredAt",
                schema: "masterdata",
                table: "ImportAuditEvents",
                columns: new[] { "TenantId", "BatchId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportAuditEvents_TenantId_BatchId_RowId",
                schema: "masterdata",
                table: "ImportAuditEvents",
                columns: new[] { "TenantId", "BatchId", "RowId" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_TenantId_Id",
                schema: "masterdata",
                table: "ImportBatches",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_TenantId_SubmittedActorId_ResourceKind_IdempotencyKey",
                schema: "masterdata",
                table: "ImportBatches",
                columns: new[] { "TenantId", "SubmittedActorId", "ResourceKind", "IdempotencyKey" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportRows_TenantId_BatchId_OriginalRowNumber_ReplaySequence",
                schema: "masterdata",
                table: "ImportRows",
                columns: new[] { "TenantId", "BatchId", "OriginalRowNumber", "ReplaySequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportRows_TenantId_BatchId_ReplayIdempotencyKey",
                schema: "masterdata",
                table: "ImportRows",
                columns: new[] { "TenantId", "BatchId", "ReplayIdempotencyKey" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportRows_TenantId_Id",
                schema: "masterdata",
                table: "ImportRows",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTermInstallments_TenantId_Id",
                schema: "masterdata",
                table: "PaymentTermInstallments",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTermInstallments_TenantId_PaymentTermVersionId_Sequence",
                schema: "masterdata",
                table: "PaymentTermInstallments",
                columns: new[] { "TenantId", "PaymentTermVersionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerms_TenantId_CodeKey",
                schema: "masterdata",
                table: "PaymentTerms",
                columns: new[] { "TenantId", "CodeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerms_TenantId_Id",
                schema: "masterdata",
                table: "PaymentTerms",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerms_TenantId_NameKey",
                schema: "masterdata",
                table: "PaymentTerms",
                columns: new[] { "TenantId", "NameKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTermVersions_TenantId_Id",
                schema: "masterdata",
                table: "PaymentTermVersions",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTermVersions_TenantId_PaymentTermId_EffectiveFrom",
                schema: "masterdata",
                table: "PaymentTermVersions",
                columns: new[] { "TenantId", "PaymentTermId", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTermVersions_TenantId_PaymentTermId_VersionNumber",
                schema: "masterdata",
                table: "PaymentTermVersions",
                columns: new[] { "TenantId", "PaymentTermId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceListPrices_TenantId_Id",
                schema: "masterdata",
                table: "PriceListPrices",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceListPrices_TenantId_PriceListId_ProductId_UnitOfMeasureId_EffectiveFrom",
                schema: "masterdata",
                table: "PriceListPrices",
                columns: new[] { "TenantId", "PriceListId", "ProductId", "UnitOfMeasureId", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceListPrices_TenantId_PriceListId_VersionNumber",
                schema: "masterdata",
                table: "PriceListPrices",
                columns: new[] { "TenantId", "PriceListId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceListPrices_TenantId_ProductId",
                schema: "masterdata",
                table: "PriceListPrices",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_PriceListPrices_TenantId_UnitOfMeasureId",
                schema: "masterdata",
                table: "PriceListPrices",
                columns: new[] { "TenantId", "UnitOfMeasureId" });

            migrationBuilder.CreateIndex(
                name: "IX_PriceLists_TenantId_CodeKey",
                schema: "masterdata",
                table: "PriceLists",
                columns: new[] { "TenantId", "CodeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceLists_TenantId_CurrencyId",
                schema: "masterdata",
                table: "PriceLists",
                columns: new[] { "TenantId", "CurrencyId" });

            migrationBuilder.CreateIndex(
                name: "IX_PriceLists_TenantId_Id",
                schema: "masterdata",
                table: "PriceLists",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductBarcodes_TenantId_ComparisonKey",
                schema: "masterdata",
                table: "ProductBarcodes",
                columns: new[] { "TenantId", "ComparisonKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductBarcodes_TenantId_ProductId",
                schema: "masterdata",
                table: "ProductBarcodes",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId_BaseUnitOfMeasureId",
                schema: "masterdata",
                table: "Products",
                columns: new[] { "TenantId", "BaseUnitOfMeasureId" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId_CategoryId",
                schema: "masterdata",
                table: "Products",
                columns: new[] { "TenantId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId_Id",
                schema: "masterdata",
                table: "Products",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId_SkuKey",
                schema: "masterdata",
                table: "Products",
                columns: new[] { "TenantId", "SkuKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Taxes_TenantId_CodeKey",
                schema: "masterdata",
                table: "Taxes",
                columns: new[] { "TenantId", "CodeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Taxes_TenantId_Id",
                schema: "masterdata",
                table: "Taxes",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Taxes_TenantId_NameKey",
                schema: "masterdata",
                table: "Taxes",
                columns: new[] { "TenantId", "NameKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxRateVersions_TenantId_Id",
                schema: "masterdata",
                table: "TaxRateVersions",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxRateVersions_TenantId_TaxId_EffectiveFrom",
                schema: "masterdata",
                table: "TaxRateVersions",
                columns: new[] { "TenantId", "TaxId", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxRateVersions_TenantId_TaxId_VersionNumber",
                schema: "masterdata",
                table: "TaxRateVersions",
                columns: new[] { "TenantId", "TaxId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitConversions_TenantId_FromUnitOfMeasureId_ToUnitOfMeasureId",
                schema: "masterdata",
                table: "UnitConversions",
                columns: new[] { "TenantId", "FromUnitOfMeasureId", "ToUnitOfMeasureId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitConversions_TenantId_ToUnitOfMeasureId",
                schema: "masterdata",
                table: "UnitConversions",
                columns: new[] { "TenantId", "ToUnitOfMeasureId" });

            migrationBuilder.CreateIndex(
                name: "IX_UnitsOfMeasure_TenantId_Code",
                schema: "masterdata",
                table: "UnitsOfMeasure",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitsOfMeasure_TenantId_Id",
                schema: "masterdata",
                table: "UnitsOfMeasure",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitsOfMeasure_TenantId_NameKey",
                schema: "masterdata",
                table: "UnitsOfMeasure",
                columns: new[] { "TenantId", "NameKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEvents",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "ExchangeRateVersions",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "ImportAuditEvents",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "ImportRows",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "PaymentTermInstallments",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "PriceListPrices",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "ProductBarcodes",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "TaxRateVersions",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "UnitConversions",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "ExchangeRates",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "ImportBatches",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "PaymentTermVersions",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "PriceLists",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "Products",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "Taxes",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "PaymentTerms",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "Currencies",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "Categories",
                schema: "masterdata");

            migrationBuilder.DropTable(
                name: "UnitsOfMeasure",
                schema: "masterdata");
        }
    }
}
