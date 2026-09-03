using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelReimbursement.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class UnifyMeetingRecordItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MeetingRecordItems_MeetingRecordId_Kind_SortOrder",
                table: "MeetingRecordItems");

            migrationBuilder.Sql(
                """
                UPDATE "MeetingRecordItems" AS item
                SET "SortOrder" = ordered."NewSortOrder"
                FROM (
                    SELECT
                        "Id",
                        (ROW_NUMBER() OVER (
                            PARTITION BY "MeetingRecordId"
                            ORDER BY
                                CASE "Kind"
                                    WHEN 'Requirement' THEN 0
                                    WHEN 'WorkFocus' THEN 1
                                    ELSE 2
                                END,
                                "SortOrder",
                                "Id") - 1)::integer AS "NewSortOrder"
                    FROM "MeetingRecordItems"
                ) AS ordered
                WHERE item."Id" = ordered."Id";
                """);

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "MeetingRecordItems");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingRecordItems_MeetingRecordId_SortOrder",
                table: "MeetingRecordItems",
                columns: new[] { "MeetingRecordId", "SortOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MeetingRecordItems_MeetingRecordId_SortOrder",
                table: "MeetingRecordItems");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "MeetingRecordItems",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Requirement");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingRecordItems_MeetingRecordId_Kind_SortOrder",
                table: "MeetingRecordItems",
                columns: new[] { "MeetingRecordId", "Kind", "SortOrder" },
                unique: true);
        }
    }
}
