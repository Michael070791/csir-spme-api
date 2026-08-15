using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Csir.Spme.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HrOrganizationCommunications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MemoAcknowledgements",
                schema: "comms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoAcknowledgements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemoAcknowledgements_Memos_MemoId",
                        column: x => x.MemoId,
                        principalSchema: "comms",
                        principalTable: "Memos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MemoAudiences",
                schema: "comms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AudienceType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DivisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RoleCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoAudiences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemoAudiences_Memos_MemoId",
                        column: x => x.MemoId,
                        principalSchema: "comms",
                        principalTable: "Memos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sections_DivisionId",
                schema: "org",
                table: "Sections",
                column: "DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_ScopeType_InstituteId_HolidayDate_Name",
                schema: "leave",
                table: "Holidays",
                columns: new[] { "ScopeType", "InstituteId", "HolidayDate", "Name" },
                unique: true,
                filter: "[InstituteId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MemoAcknowledgements_EmployeeId",
                schema: "comms",
                table: "MemoAcknowledgements",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_MemoAcknowledgements_MemoId_EmployeeId",
                schema: "comms",
                table: "MemoAcknowledgements",
                columns: new[] { "MemoId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemoAudiences_DivisionId",
                schema: "comms",
                table: "MemoAudiences",
                column: "DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_MemoAudiences_EmployeeId",
                schema: "comms",
                table: "MemoAudiences",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_MemoAudiences_InstituteId",
                schema: "comms",
                table: "MemoAudiences",
                column: "InstituteId");

            migrationBuilder.CreateIndex(
                name: "IX_MemoAudiences_MemoId_AudienceType",
                schema: "comms",
                table: "MemoAudiences",
                columns: new[] { "MemoId", "AudienceType" });

            migrationBuilder.CreateIndex(
                name: "IX_MemoAudiences_SectionId",
                schema: "comms",
                table: "MemoAudiences",
                column: "SectionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Sections_Divisions_DivisionId",
                schema: "org",
                table: "Sections",
                column: "DivisionId",
                principalSchema: "org",
                principalTable: "Divisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sections_Divisions_DivisionId",
                schema: "org",
                table: "Sections");

            migrationBuilder.DropTable(
                name: "MemoAcknowledgements",
                schema: "comms");

            migrationBuilder.DropTable(
                name: "MemoAudiences",
                schema: "comms");

            migrationBuilder.DropIndex(
                name: "IX_Sections_DivisionId",
                schema: "org",
                table: "Sections");

            migrationBuilder.DropIndex(
                name: "IX_Holidays_ScopeType_InstituteId_HolidayDate_Name",
                schema: "leave",
                table: "Holidays");
        }
    }
}
