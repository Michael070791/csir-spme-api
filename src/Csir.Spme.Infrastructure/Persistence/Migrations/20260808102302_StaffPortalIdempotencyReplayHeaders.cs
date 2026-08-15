using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Csir.Spme.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StaffPortalIdempotencyReplayHeaders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResponseEtag",
                schema: "ops",
                table: "IdempotencyRecords",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponseLocation",
                schema: "ops",
                table: "IdempotencyRecords",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResponseEtag",
                schema: "ops",
                table: "IdempotencyRecords");

            migrationBuilder.DropColumn(
                name: "ResponseLocation",
                schema: "ops",
                table: "IdempotencyRecords");
        }
    }
}
