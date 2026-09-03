using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelReimbursement.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectMeetingRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MeetingRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    LastEditedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedById = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingRecords", x => x.Id);
                    table.CheckConstraint("CK_MeetingRecords_DeletedState", "(\"DeletedAt\" IS NULL AND \"DeletedById\" IS NULL) OR (\"DeletedAt\" IS NOT NULL AND \"DeletedById\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_MeetingRecords_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeetingRecords_AspNetUsers_DeletedById",
                        column: x => x.DeletedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeetingRecords_AspNetUsers_LastEditedById",
                        column: x => x.LastEditedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeetingRecords_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MeetingParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Organization = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingParticipants_MeetingRecords_MeetingRecordId",
                        column: x => x.MeetingRecordId,
                        principalTable: "MeetingRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingRecordItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MeetingRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Owner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingRecordItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingRecordItems_MeetingRecords_MeetingRecordId",
                        column: x => x.MeetingRecordId,
                        principalTable: "MeetingRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingParticipants_MeetingRecordId_SortOrder",
                table: "MeetingParticipants",
                columns: new[] { "MeetingRecordId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingRecordItems_MeetingRecordId_Kind_SortOrder",
                table: "MeetingRecordItems",
                columns: new[] { "MeetingRecordId", "Kind", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingRecords_CreatedById",
                table: "MeetingRecords",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingRecords_DeletedById",
                table: "MeetingRecords",
                column: "DeletedById");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingRecords_LastEditedById",
                table: "MeetingRecords",
                column: "LastEditedById");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingRecords_MeetingDate_CreatedAt",
                table: "MeetingRecords",
                columns: new[] { "MeetingDate", "CreatedAt" },
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingRecords_ProjectId_MeetingDate_CreatedAt",
                table: "MeetingRecords",
                columns: new[] { "ProjectId", "MeetingDate", "CreatedAt" },
                filter: "\"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeetingParticipants");

            migrationBuilder.DropTable(
                name: "MeetingRecordItems");

            migrationBuilder.DropTable(
                name: "MeetingRecords");
        }
    }
}
