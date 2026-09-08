using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelReimbursement.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAttachmentPurpose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "AttachmentAssets",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Invoice");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "AttachmentAssets");
        }
    }
}
