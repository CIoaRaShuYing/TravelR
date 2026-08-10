using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelReimbursement.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMealAllowanceWeeklyReportsAndBankProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankCardLastFour",
                table: "PayoutRecords",
                type: "character varying(4)",
                maxLength: 4,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RecipientName",
                table: "PayoutRecords",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BankCardProtected",
                table: "AspNetUsers",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonalName",
                table: "AspNetUsers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MealAllowances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartureDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReturnDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Days = table.Column<int>(type: "integer", nullable: false),
                    DailyAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PayoutStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ReviewedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewComment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealAllowances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealAllowances_ClaimVersions_ClaimVersionId",
                        column: x => x.ClaimVersionId,
                        principalTable: "ClaimVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WeeklyReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    WeekStart = table.Column<DateOnly>(type: "date", nullable: false),
                    CompletedWork = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    NextWeekPlan = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Issues = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    LastEditedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeeklyReports_AspNetUsers_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeeklyReports_AspNetUsers_LastEditedById",
                        column: x => x.LastEditedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeeklyReports_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MealAllowanceApprovalRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MealAllowanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ToStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DailyAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealAllowanceApprovalRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealAllowanceApprovalRecords_MealAllowances_MealAllowanceId",
                        column: x => x.MealAllowanceId,
                        principalTable: "MealAllowances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MealAllowancePayoutRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MealAllowanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RecipientName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BankCardLastFour = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    ConfirmedById = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealAllowancePayoutRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealAllowancePayoutRecords_MealAllowances_MealAllowanceId",
                        column: x => x.MealAllowanceId,
                        principalTable: "MealAllowances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MealAllowanceApprovalRecords_MealAllowanceId_CreatedAt",
                table: "MealAllowanceApprovalRecords",
                columns: new[] { "MealAllowanceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MealAllowancePayoutRecords_MealAllowanceId",
                table: "MealAllowancePayoutRecords",
                column: "MealAllowanceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MealAllowances_ClaimVersionId",
                table: "MealAllowances",
                column: "ClaimVersionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MealAllowances_PayoutStatus_UpdatedAt",
                table: "MealAllowances",
                columns: new[] { "PayoutStatus", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MealAllowances_Status_UpdatedAt",
                table: "MealAllowances",
                columns: new[] { "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyReports_AuthorId_ProjectId_WeekStart",
                table: "WeeklyReports",
                columns: new[] { "AuthorId", "ProjectId", "WeekStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyReports_LastEditedById",
                table: "WeeklyReports",
                column: "LastEditedById");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyReports_ProjectId_WeekStart",
                table: "WeeklyReports",
                columns: new[] { "ProjectId", "WeekStart" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MealAllowanceApprovalRecords");

            migrationBuilder.DropTable(
                name: "MealAllowancePayoutRecords");

            migrationBuilder.DropTable(
                name: "WeeklyReports");

            migrationBuilder.DropTable(
                name: "MealAllowances");

            migrationBuilder.DropColumn(
                name: "BankCardLastFour",
                table: "PayoutRecords");

            migrationBuilder.DropColumn(
                name: "RecipientName",
                table: "PayoutRecords");

            migrationBuilder.DropColumn(
                name: "BankCardProtected",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PersonalName",
                table: "AspNetUsers");
        }
    }
}
