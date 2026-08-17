using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Csir.Spme.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowApprovalTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkflowApprovalTokens",
                schema: "leave",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApproverUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowApprovalTokens", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowApprovalTokens_ExpiresAt",
                schema: "leave",
                table: "WorkflowApprovalTokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowApprovalTokens_ResourceId_Purpose_Stage_ExpiresAt",
                schema: "leave",
                table: "WorkflowApprovalTokens",
                columns: new[] { "ResourceId", "Purpose", "Stage", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowApprovalTokens_TokenHash",
                schema: "leave",
                table: "WorkflowApprovalTokens",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkflowApprovalTokens",
                schema: "leave");
        }
    }
}
