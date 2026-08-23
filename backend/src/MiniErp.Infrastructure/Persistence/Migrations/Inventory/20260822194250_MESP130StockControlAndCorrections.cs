using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Inventory
{
    /// <inheritdoc />
    public partial class MESP130StockControlAndCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Adjustments",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    WarehouseName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RequesterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ApprovalPolicySnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32768, nullable: true),
                    CurrentApprovalStageIndex = table.Column<int>(type: "int", nullable: false),
                    CurrentStageApprovalCount = table.Column<int>(type: "int", nullable: false),
                    LastApproverId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastDelegatedFromActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PostedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Adjustments", x => x.Id);
                    table.UniqueConstraint("AK_Adjustments_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "ControlHistory",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: false),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DelegatedFromActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RoundGeneration = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Counts",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    WarehouseName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CountType = table.Column<int>(type: "int", nullable: false),
                    AssignedCounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApproverId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PosterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CurrentRoundGeneration = table.Column<int>(type: "int", nullable: false),
                    SnapshotCutoff = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PostedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Counts", x => x.Id);
                    table.UniqueConstraint("AK_Counts_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "ReasonCodes",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EnglishName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ArabicName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReasonCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockIssues",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    WarehouseName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DestinationUseDescription = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    RequesterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ApprovalPolicySnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 32768, nullable: true),
                    CurrentApprovalStageIndex = table.Column<int>(type: "int", nullable: false),
                    CurrentStageApprovalCount = table.Column<int>(type: "int", nullable: false),
                    LastApproverId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastDelegatedFromActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PostedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockIssues", x => x.Id);
                    table.UniqueConstraint("AK_StockIssues_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "AdjustmentLines",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdjustmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductSku = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitOfMeasureCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    TrackingIdentity = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ReasonCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ReasonEnglishName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ReasonArabicName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    MovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdjustmentLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdjustmentLines_Adjustments_TenantId_AdjustmentId",
                        columns: x => new { x.TenantId, x.AdjustmentId },
                        principalSchema: "inventory",
                        principalTable: "Adjustments",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CountLines",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PriorLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RoundGeneration = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductSku = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitOfMeasureCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TrackingIdentity = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ExpectedQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    CountedQuantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    Variance = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: true),
                    VarianceReasonCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VarianceReasonCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    VarianceReasonEnglishName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    VarianceReasonArabicName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CountedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    MovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CountLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CountLines_Counts_TenantId_CountId",
                        columns: x => new { x.TenantId, x.CountId },
                        principalSchema: "inventory",
                        principalTable: "Counts",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockIssueLines",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StockIssueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductSku = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitOfMeasureCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false),
                    TrackingIdentity = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ReasonCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ReasonEnglishName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ReasonArabicName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EvidenceReference = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    MovementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockIssueLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockIssueLines_StockIssues_TenantId_StockIssueId",
                        columns: x => new { x.TenantId, x.StockIssueId },
                        principalSchema: "inventory",
                        principalTable: "StockIssues",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdjustmentLines_TenantId_AdjustmentId",
                schema: "inventory",
                table: "AdjustmentLines",
                columns: new[] { "TenantId", "AdjustmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AdjustmentLines_TenantId_AdjustmentId_Id",
                schema: "inventory",
                table: "AdjustmentLines",
                columns: new[] { "TenantId", "AdjustmentId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdjustmentLines_TenantId_Id",
                schema: "inventory",
                table: "AdjustmentLines",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Adjustments_TenantId_CompanyId_BranchId_WarehouseId_Status_CreatedAt",
                schema: "inventory",
                table: "Adjustments",
                columns: new[] { "TenantId", "CompanyId", "BranchId", "WarehouseId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Adjustments_TenantId_Id",
                schema: "inventory",
                table: "Adjustments",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ControlHistory_TenantId_Id",
                schema: "inventory",
                table: "ControlHistory",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ControlHistory_TenantId_ResourceType_ResourceId_OccurredAt",
                schema: "inventory",
                table: "ControlHistory",
                columns: new[] { "TenantId", "ResourceType", "ResourceId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CountLines_TenantId_CountId_Id",
                schema: "inventory",
                table: "CountLines",
                columns: new[] { "TenantId", "CountId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CountLines_TenantId_CountId_RoundGeneration",
                schema: "inventory",
                table: "CountLines",
                columns: new[] { "TenantId", "CountId", "RoundGeneration" });

            migrationBuilder.CreateIndex(
                name: "IX_CountLines_TenantId_Id",
                schema: "inventory",
                table: "CountLines",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Counts_TenantId_CompanyId_BranchId_WarehouseId_Status_CreatedAt",
                schema: "inventory",
                table: "Counts",
                columns: new[] { "TenantId", "CompanyId", "BranchId", "WarehouseId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Counts_TenantId_Id",
                schema: "inventory",
                table: "Counts",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReasonCodes_TenantId_Code",
                schema: "inventory",
                table: "ReasonCodes",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReasonCodes_TenantId_Id",
                schema: "inventory",
                table: "ReasonCodes",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockIssueLines_TenantId_Id",
                schema: "inventory",
                table: "StockIssueLines",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockIssueLines_TenantId_StockIssueId",
                schema: "inventory",
                table: "StockIssueLines",
                columns: new[] { "TenantId", "StockIssueId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockIssueLines_TenantId_StockIssueId_Id",
                schema: "inventory",
                table: "StockIssueLines",
                columns: new[] { "TenantId", "StockIssueId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockIssues_TenantId_CompanyId_BranchId_WarehouseId_Status_CreatedAt",
                schema: "inventory",
                table: "StockIssues",
                columns: new[] { "TenantId", "CompanyId", "BranchId", "WarehouseId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StockIssues_TenantId_Id",
                schema: "inventory",
                table: "StockIssues",
                columns: new[] { "TenantId", "Id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdjustmentLines",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "ControlHistory",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "CountLines",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "ReasonCodes",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "StockIssueLines",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "Adjustments",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "Counts",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "StockIssues",
                schema: "inventory");
        }
    }
}
