using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErp.Infrastructure.Persistence.Migrations.BusinessParties
{
    /// <inheritdoc />
    public partial class SharedTenantRuntimeModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // TenantOwnedRecords is created and upgraded only by the dedicated
            // Foundation tenancy migration. This runtime-model alignment
            // migration records the shared entity in this module snapshot
            // without competing for ownership of the physical table.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The shared Foundation table must remain owned by the tenancy
            // migration even when a module migration is rolled back.
        }
    }
}
