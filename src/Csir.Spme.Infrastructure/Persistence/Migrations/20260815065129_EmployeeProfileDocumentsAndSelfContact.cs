using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Csir.Spme.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeProfileDocumentsAndSelfContact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('hr.Employees', 'Address') IS NULL
                    ALTER TABLE hr.Employees ADD Address nvarchar(512) NULL;
                IF COL_LENGTH('hr.Employees', 'ResidentialAddress') IS NOT NULL
                    ALTER TABLE hr.Employees DROP COLUMN ResidentialAddress;
                """);

            migrationBuilder.CreateTable(
                name: "EmployeeDocuments",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LinkedChildId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeDocuments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "hr",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeDocumentUploadSessions",
                schema: "hr",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstituteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InitiatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LinkedChildId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StorageKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DeclaredSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    DeclaredSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EmployeeDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeDocumentUploadSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_EmployeeId_DocumentType_LinkedChildId_Status",
                schema: "hr",
                table: "EmployeeDocuments",
                columns: new[] { "EmployeeId", "DocumentType", "LinkedChildId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_EmployeeId_DocumentType_Status",
                schema: "hr",
                table: "EmployeeDocuments",
                columns: new[] { "EmployeeId", "DocumentType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocumentUploadSessions_EmployeeId_Status",
                schema: "hr",
                table: "EmployeeDocumentUploadSessions",
                columns: new[] { "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocumentUploadSessions_StorageKey",
                schema: "hr",
                table: "EmployeeDocumentUploadSessions",
                column: "StorageKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeDocuments",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "EmployeeDocumentUploadSessions",
                schema: "hr");
        }
    }
}
