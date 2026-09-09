using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelReimbursement.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimArchiveBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ArchiveBatchId",
                table: "ReimbursementClaims",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClaimArchiveBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SubmittedFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    SubmittedTo = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimArchiveBatches", x => x.Id);
                    table.CheckConstraint("CK_ClaimArchiveBatches_SubmittedRange", "\"SubmittedTo\" >= \"SubmittedFrom\"");
                    table.ForeignKey(
                        name: "FK_ClaimArchiveBatches_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReimbursementClaims_ArchiveBatchId_SubmittedAt",
                table: "ReimbursementClaims",
                columns: new[] { "ArchiveBatchId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimArchiveBatches_CreatedAt",
                table: "ClaimArchiveBatches",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimArchiveBatches_CreatedById",
                table: "ClaimArchiveBatches",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimArchiveBatches_NormalizedName",
                table: "ClaimArchiveBatches",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ReimbursementClaims_ClaimArchiveBatches_ArchiveBatchId",
                table: "ReimbursementClaims",
                column: "ArchiveBatchId",
                principalTable: "ClaimArchiveBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReimbursementClaims_ClaimArchiveBatches_ArchiveBatchId",
                table: "ReimbursementClaims");

            migrationBuilder.DropTable(
                name: "ClaimArchiveBatches");

            migrationBuilder.DropIndex(
                name: "IX_ReimbursementClaims_ArchiveBatchId_SubmittedAt",
                table: "ReimbursementClaims");

            migrationBuilder.DropColumn(
                name: "ArchiveBatchId",
                table: "ReimbursementClaims");
        }
    }
}
