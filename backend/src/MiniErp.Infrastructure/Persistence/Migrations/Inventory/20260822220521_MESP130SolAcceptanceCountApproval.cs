using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Inventory
{
    /// <inheritdoc />
    public partial class MESP130SolAcceptanceCountApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalPolicySnapshotJson",
                schema: "inventory",
                table: "Counts",
                type: "nvarchar(max)",
                maxLength: 32768,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentApprovalStageIndex",
                schema: "inventory",
                table: "Counts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CurrentStageApprovalCount",
                schema: "inventory",
                table: "Counts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CurrentStageApproverIdsJson",
                schema: "inventory",
                table: "Counts",
                type: "nvarchar(max)",
                maxLength: 32768,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastApproverId",
                schema: "inventory",
                table: "Counts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastDelegatedFromActorId",
                schema: "inventory",
                table: "Counts",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovalPolicySnapshotJson",
                schema: "inventory",
                table: "Counts");

            migrationBuilder.DropColumn(
                name: "CurrentApprovalStageIndex",
                schema: "inventory",
                table: "Counts");

            migrationBuilder.DropColumn(
                name: "CurrentStageApprovalCount",
                schema: "inventory",
                table: "Counts");

            migrationBuilder.DropColumn(
                name: "CurrentStageApproverIdsJson",
                schema: "inventory",
                table: "Counts");

            migrationBuilder.DropColumn(
                name: "LastApproverId",
                schema: "inventory",
                table: "Counts");

            migrationBuilder.DropColumn(
                name: "LastDelegatedFromActorId",
                schema: "inventory",
                table: "Counts");
        }
    }
}
