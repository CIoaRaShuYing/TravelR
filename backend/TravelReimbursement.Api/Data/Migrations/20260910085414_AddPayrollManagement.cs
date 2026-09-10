using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelReimbursement.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PayrollMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LockedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollPeriods", x => x.Id);
                    table.CheckConstraint("CK_PayrollPeriods_PayrollMonth", "EXTRACT(DAY FROM \"PayrollMonth\") = 1");
                    table.ForeignKey(
                        name: "FK_PayrollPeriods_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PayrollPeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeDisplayNameSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EmployeePersonalNameSnapshot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BankCardLastFourSnapshot = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    BaseSalary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PerformanceSalary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Bonus = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Allowance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherIncrease = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SocialSecurityDeduction = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HousingFundDeduction = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IndividualIncomeTax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OtherDeduction = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GrossPay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDeductions = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    NetPay = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PayoutStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollEntries", x => x.Id);
                    table.CheckConstraint("CK_PayrollEntries_Amounts", "\"BaseSalary\" >= 0 AND \"PerformanceSalary\" >= 0 AND \"Bonus\" >= 0 AND \"Allowance\" >= 0 AND \"OtherIncrease\" >= 0 AND \"SocialSecurityDeduction\" >= 0 AND \"HousingFundDeduction\" >= 0 AND \"IndividualIncomeTax\" >= 0 AND \"OtherDeduction\" >= 0 AND \"GrossPay\" >= 0 AND \"TotalDeductions\" >= 0 AND \"NetPay\" >= 0");
                    table.CheckConstraint("CK_PayrollEntries_GrossPay", "\"GrossPay\" = \"BaseSalary\" + \"PerformanceSalary\" + \"Bonus\" + \"Allowance\" + \"OtherIncrease\"");
                    table.CheckConstraint("CK_PayrollEntries_NetPay", "\"NetPay\" = \"GrossPay\" - \"TotalDeductions\"");
                    table.CheckConstraint("CK_PayrollEntries_TotalDeductions", "\"TotalDeductions\" = \"SocialSecurityDeduction\" + \"HousingFundDeduction\" + \"IndividualIncomeTax\" + \"OtherDeduction\"");
                    table.ForeignKey(
                        name: "FK_PayrollEntries_AspNetUsers_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollEntries_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollEntries_PayrollPeriods_PayrollPeriodId",
                        column: x => x.PayrollPeriodId,
                        principalTable: "PayrollPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollPayoutRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PayrollEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayrollMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RecipientName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BankCardLastFour = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    ConfirmedById = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollPayoutRecords", x => x.Id);
                    table.CheckConstraint("CK_PayrollPayoutRecords_BankCardLastFour", "\"BankCardLastFour\" ~ '^[0-9]{4}$'");
                    table.ForeignKey(
                        name: "FK_PayrollPayoutRecords_AspNetUsers_ConfirmedById",
                        column: x => x.ConfirmedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollPayoutRecords_PayrollEntries_PayrollEntryId",
                        column: x => x.PayrollEntryId,
                        principalTable: "PayrollEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntries_PayrollPeriodId_PayoutStatus",
                table: "PayrollEntries",
                columns: new[] { "PayrollPeriodId", "PayoutStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntries_PayrollPeriodId_UserId",
                table: "PayrollEntries",
                columns: new[] { "PayrollPeriodId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntries_UpdatedById",
                table: "PayrollEntries",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntries_UserId_PayoutStatus_PayrollPeriodId",
                table: "PayrollEntries",
                columns: new[] { "UserId", "PayoutStatus", "PayrollPeriodId" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPayoutRecords_ConfirmedById",
                table: "PayrollPayoutRecords",
                column: "ConfirmedById");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPayoutRecords_PayrollEntryId",
                table: "PayrollPayoutRecords",
                column: "PayrollEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriods_CreatedById",
                table: "PayrollPeriods",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPeriods_PayrollMonth",
                table: "PayrollPeriods",
                column: "PayrollMonth",
                unique: true,
                filter: "\"Status\" <> 'Cancelled'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollPayoutRecords");

            migrationBuilder.DropTable(
                name: "PayrollEntries");

            migrationBuilder.DropTable(
                name: "PayrollPeriods");
        }
    }
}
