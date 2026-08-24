using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Inventory
{
    /// <inheritdoc />
    public partial class MESP131SolFinancialIntegrityRemediation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ValuationStates_TenantId_PolicyId_CompanyId_BranchId_WarehouseId_ProductId_UnitOfMeasureId_TrackingIdentity",
                schema: "inventory",
                table: "ValuationStates");

            migrationBuilder.DropIndex(
                name: "IX_ValuationScopeAnchors_TenantId_PolicyId_CompanyId_BranchId_WarehouseId_ProductId_UnitOfMeasureId_TrackingIdentity",
                schema: "inventory",
                table: "ValuationScopeAnchors");

            migrationBuilder.DropColumn(
                name: "PolicyId",
                schema: "inventory",
                table: "ValuationScopeAnchors");

            migrationBuilder.RenameColumn(
                name: "PolicyId",
                schema: "inventory",
                table: "ValuationStates",
                newName: "CurrentPolicyId");

            migrationBuilder.RenameColumn(
                name: "PolicyVersionNumber",
                schema: "inventory",
                table: "ValuationStates",
                newName: "CurrentPolicyVersionNumber");

            migrationBuilder.AlterColumn<Guid>(
                name: "CurrentPolicyId",
                schema: "inventory",
                table: "ValuationStates",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<int>(
                name: "CurrentPolicyVersionNumber",
                schema: "inventory",
                table: "ValuationStates",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<Guid>(
                name: "SupersedesPolicyId",
                schema: "inventory",
                table: "ValuationPolicies",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "UnitCostScale",
                schema: "inventory",
                table: "MovementValuationEvents",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "RoundingMode",
                schema: "inventory",
                table: "MovementValuationEvents",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "PolicyVersionNumber",
                schema: "inventory",
                table: "MovementValuationEvents",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<Guid>(
                name: "PolicyId",
                schema: "inventory",
                table: "MovementValuationEvents",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "FunctionalCurrencyCode",
                schema: "inventory",
                table: "MovementValuationEvents",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<int>(
                name: "AmountScale",
                schema: "inventory",
                table: "MovementValuationEvents",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "Direction",
                schema: "inventory",
                table: "FinanceValuationHandoffs",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "SignedBaseAmount",
                schema: "inventory",
                table: "FinanceValuationHandoffs",
                type: "decimal(28,8)",
                precision: 28,
                scale: 8,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql("""
                ;WITH NumberedPolicies AS
                (
                    SELECT Id,
                           ROW_NUMBER() OVER (PARTITION BY TenantId, CompanyId ORDER BY EffectiveFrom, Id) AS VersionNumber
                    FROM inventory.ValuationPolicies
                )
                UPDATE policy
                SET VersionNumber = numbered.VersionNumber
                FROM inventory.ValuationPolicies AS policy
                INNER JOIN NumberedPolicies AS numbered ON numbered.Id = policy.Id;
                """);

            migrationBuilder.Sql("""
                UPDATE handoff
                SET BaseAmount = ABS(handoff.BaseAmount),
                    Direction = COALESCE(movement.Direction, 1),
                    SignedBaseAmount = CASE
                        WHEN COALESCE(movement.Direction, 1) = 1 THEN ABS(handoff.BaseAmount)
                        ELSE -ABS(handoff.BaseAmount)
                    END
                FROM inventory.FinanceValuationHandoffs AS handoff
                LEFT JOIN inventory.MovementValuationEvents AS movement
                    ON movement.TenantId = handoff.TenantId
                   AND movement.MovementId = handoff.MovementId
                   AND movement.Status = 2;
                """);

            migrationBuilder.Sql("""
                ;WITH DuplicateStates AS
                (
                    SELECT Id,
                           ROW_NUMBER() OVER
                           (
                               PARTITION BY TenantId, CompanyId, BranchId, WarehouseId, ProductId, UnitOfMeasureId, TrackingIdentity
                               ORDER BY LastAppliedLedgerSequence DESC, UpdatedAt DESC, Id DESC
                           ) AS DuplicateRank
                    FROM inventory.ValuationStates
                )
                DELETE state
                FROM inventory.ValuationStates AS state
                INNER JOIN DuplicateStates AS duplicate ON duplicate.Id = state.Id
                WHERE duplicate.DuplicateRank > 1;

                ;WITH DuplicateAnchors AS
                (
                    SELECT Id,
                           ROW_NUMBER() OVER
                           (
                               PARTITION BY TenantId, CompanyId, BranchId, WarehouseId, ProductId, UnitOfMeasureId, TrackingIdentity
                               ORDER BY Id DESC
                           ) AS DuplicateRank
                    FROM inventory.ValuationScopeAnchors
                )
                DELETE anchor
                FROM inventory.ValuationScopeAnchors AS anchor
                INNER JOIN DuplicateAnchors AS duplicate ON duplicate.Id = anchor.Id
                WHERE duplicate.DuplicateRank > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ValuationStates_TenantId_CompanyId_BranchId_WarehouseId_ProductId_UnitOfMeasureId_TrackingIdentity",
                schema: "inventory",
                table: "ValuationStates",
                columns: new[] { "TenantId", "CompanyId", "BranchId", "WarehouseId", "ProductId", "UnitOfMeasureId", "TrackingIdentity" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ValuationScopeAnchors_TenantId_CompanyId_BranchId_WarehouseId_ProductId_UnitOfMeasureId_TrackingIdentity",
                schema: "inventory",
                table: "ValuationScopeAnchors",
                columns: new[] { "TenantId", "CompanyId", "BranchId", "WarehouseId", "ProductId", "UnitOfMeasureId", "TrackingIdentity" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ValuationPolicies_TenantId_CompanyId_VersionNumber",
                schema: "inventory",
                table: "ValuationPolicies",
                columns: new[] { "TenantId", "CompanyId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ValuationStates_TenantId_CompanyId_BranchId_WarehouseId_ProductId_UnitOfMeasureId_TrackingIdentity",
                schema: "inventory",
                table: "ValuationStates");

            migrationBuilder.DropIndex(
                name: "IX_ValuationScopeAnchors_TenantId_CompanyId_BranchId_WarehouseId_ProductId_UnitOfMeasureId_TrackingIdentity",
                schema: "inventory",
                table: "ValuationScopeAnchors");

            migrationBuilder.DropIndex(
                name: "IX_ValuationPolicies_TenantId_CompanyId_VersionNumber",
                schema: "inventory",
                table: "ValuationPolicies");

            migrationBuilder.DropColumn(
                name: "SupersedesPolicyId",
                schema: "inventory",
                table: "ValuationPolicies");

            migrationBuilder.DropColumn(
                name: "Direction",
                schema: "inventory",
                table: "FinanceValuationHandoffs");

            migrationBuilder.DropColumn(
                name: "SignedBaseAmount",
                schema: "inventory",
                table: "FinanceValuationHandoffs");

            migrationBuilder.Sql("""
                UPDATE inventory.ValuationStates
                SET CurrentPolicyId = COALESCE(CurrentPolicyId, '00000000-0000-0000-0000-000000000000'),
                    CurrentPolicyVersionNumber = COALESCE(CurrentPolicyVersionNumber, 0);
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "CurrentPolicyId",
                schema: "inventory",
                table: "ValuationStates",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CurrentPolicyVersionNumber",
                schema: "inventory",
                table: "ValuationStates",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "CurrentPolicyId",
                schema: "inventory",
                table: "ValuationStates",
                newName: "PolicyId");

            migrationBuilder.RenameColumn(
                name: "CurrentPolicyVersionNumber",
                schema: "inventory",
                table: "ValuationStates",
                newName: "PolicyVersionNumber");

            migrationBuilder.AddColumn<Guid>(
                name: "PolicyId",
                schema: "inventory",
                table: "ValuationScopeAnchors",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<int>(
                name: "UnitCostScale",
                schema: "inventory",
                table: "MovementValuationEvents",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "RoundingMode",
                schema: "inventory",
                table: "MovementValuationEvents",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PolicyVersionNumber",
                schema: "inventory",
                table: "MovementValuationEvents",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PolicyId",
                schema: "inventory",
                table: "MovementValuationEvents",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FunctionalCurrencyCode",
                schema: "inventory",
                table: "MovementValuationEvents",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AmountScale",
                schema: "inventory",
                table: "MovementValuationEvents",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ValuationStates_TenantId_PolicyId_CompanyId_BranchId_WarehouseId_ProductId_UnitOfMeasureId_TrackingIdentity",
                schema: "inventory",
                table: "ValuationStates",
                columns: new[] { "TenantId", "PolicyId", "CompanyId", "BranchId", "WarehouseId", "ProductId", "UnitOfMeasureId", "TrackingIdentity" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ValuationScopeAnchors_TenantId_PolicyId_CompanyId_BranchId_WarehouseId_ProductId_UnitOfMeasureId_TrackingIdentity",
                schema: "inventory",
                table: "ValuationScopeAnchors",
                columns: new[] { "TenantId", "PolicyId", "CompanyId", "BranchId", "WarehouseId", "ProductId", "UnitOfMeasureId", "TrackingIdentity" },
                unique: true);
        }
    }
}
