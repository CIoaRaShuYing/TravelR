using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelReimbursement.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // One-time reset explicitly requested for the project allocation rollout.
            // Data removed here cannot be recovered by Down; restore a pre-migration backup if needed.
            migrationBuilder.Sql("""
                DELETE FROM "PayrollPayoutRecords";
                DELETE FROM "AuditLogs" WHERE "EntityType" IN ('PayrollPeriod', 'PayrollEntry', 'PayrollPayoutRecord');
                UPDATE "PayrollEntries" SET "PayoutStatus" = 'NotApplicable', "PaidAt" = NULL,
                    "EmployeePersonalNameSnapshot" = NULL, "BankCardLastFourSnapshot" = NULL,
                    "UpdatedAt" = CURRENT_TIMESTAMP, "ConcurrencyToken" = gen_random_uuid();
                UPDATE "PayrollPeriods" SET "Status" = 'Draft', "LockedAt" = NULL, "CompletedAt" = NULL,
                    "UpdatedAt" = CURRENT_TIMESTAMP, "ConcurrencyToken" = gen_random_uuid()
                    WHERE "Status" <> 'Cancelled';
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "PayrollEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntries_ProjectId",
                table: "PayrollEntries",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollEntries_Projects_ProjectId",
                table: "PayrollEntries",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PayrollEntries_Projects_ProjectId",
                table: "PayrollEntries");

            migrationBuilder.DropIndex(
                name: "IX_PayrollEntries_ProjectId",
                table: "PayrollEntries");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "PayrollEntries");
        }
    }
}
