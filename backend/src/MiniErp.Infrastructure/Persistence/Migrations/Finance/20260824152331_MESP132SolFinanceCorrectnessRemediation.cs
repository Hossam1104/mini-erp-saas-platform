using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.Finance
{
    /// <inheritdoc />
    public partial class MESP132SolFinanceCorrectnessRemediation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_Accounts_TenantId_ParentAccountId",
                schema: "finance",
                table: "Accounts");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Accounts_TenantId_Id",
                schema: "finance",
                table: "Accounts");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_TenantId_ParentAccountId",
                schema: "finance",
                table: "Accounts");

            migrationBuilder.AddColumn<int>(
                name: "AmountAuthority",
                schema: "finance",
                table: "Journals",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalRequirement",
                schema: "finance",
                table: "Journals",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Accounts_TenantId_CompanyId_Id",
                schema: "finance",
                table: "Accounts",
                columns: new[] { "TenantId", "CompanyId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_TenantId_CompanyId_ParentAccountId",
                schema: "finance",
                table: "Accounts",
                columns: new[] { "TenantId", "CompanyId", "ParentAccountId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_Accounts_TenantId_CompanyId_ParentAccountId",
                schema: "finance",
                table: "Accounts",
                columns: new[] { "TenantId", "CompanyId", "ParentAccountId" },
                principalSchema: "finance",
                principalTable: "Accounts",
                principalColumns: new[] { "TenantId", "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_Accounts_TenantId_CompanyId_ParentAccountId",
                schema: "finance",
                table: "Accounts");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Accounts_TenantId_CompanyId_Id",
                schema: "finance",
                table: "Accounts");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_TenantId_CompanyId_ParentAccountId",
                schema: "finance",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "AmountAuthority",
                schema: "finance",
                table: "Journals");

            migrationBuilder.DropColumn(
                name: "ApprovalRequirement",
                schema: "finance",
                table: "Journals");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Accounts_TenantId_Id",
                schema: "finance",
                table: "Accounts",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_TenantId_ParentAccountId",
                schema: "finance",
                table: "Accounts",
                columns: new[] { "TenantId", "ParentAccountId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_Accounts_TenantId_ParentAccountId",
                schema: "finance",
                table: "Accounts",
                columns: new[] { "TenantId", "ParentAccountId" },
                principalSchema: "finance",
                principalTable: "Accounts",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
