using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Csir.Spme.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SecureStaffIdentityAndCommunications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "IssuedAt",
                schema: "iam",
                table: "RefreshTokens",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "RevocationReason",
                schema: "iam",
                table: "RefreshTokens",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                schema: "iam",
                table: "RefreshTokens",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                "UPDATE [iam].[RefreshTokens] SET " +
                "[IssuedAt] = SYSUTCDATETIME(), " +
                "[RevokedAt] = COALESCE([RevokedAt], SYSUTCDATETIME()), " +
                "[RevocationReason] = COALESCE([RevocationReason], N'legacy-token-invalidated') " +
                "WHERE [SecurityStamp] = N'';");

            migrationBuilder.CreateTable(
                name: "AccountActivationChallenges",
                schema: "iam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedIdentifierHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DeliveryChannel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    DestinationHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OtpHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    VerificationTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    MaximumAttempts = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountActivationChallenges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationOutboxMessages",
                schema: "comms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Recipient = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                    IsHtml = table.Column<bool>(type: "bit", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LockedUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastErrorCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationOutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserLoginIdentifiers",
                schema: "iam",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdentifierType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    NormalizedValue = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    VerificationSource = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLoginIdentifiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserLoginIdentifiers_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "iam",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommunicationDeliveryAttempts",
                schema: "comms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OutboxMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    HttpStatusCode = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationDeliveryAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunicationDeliveryAttempts_CommunicationOutboxMessages_OutboxMessageId",
                        column: x => x.OutboxMessageId,
                        principalSchema: "comms",
                        principalTable: "CommunicationOutboxMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_FamilyId_RevokedAt",
                schema: "iam",
                table: "RefreshTokens",
                columns: new[] { "FamilyId", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountActivationChallenges_RequestedIdentifierHash_CreatedAt",
                schema: "iam",
                table: "AccountActivationChallenges",
                columns: new[] { "RequestedIdentifierHash", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountActivationChallenges_UserId_ExpiresAt",
                schema: "iam",
                table: "AccountActivationChallenges",
                columns: new[] { "UserId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationDeliveryAttempts_OutboxMessageId_AttemptNumber",
                schema: "comms",
                table: "CommunicationDeliveryAttempts",
                columns: new[] { "OutboxMessageId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationOutboxMessages_IdempotencyKey",
                schema: "comms",
                table: "CommunicationOutboxMessages",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationOutboxMessages_Status_NextAttemptAt",
                schema: "comms",
                table: "CommunicationOutboxMessages",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserLoginIdentifiers_EmployeeId_IsActive",
                schema: "iam",
                table: "UserLoginIdentifiers",
                columns: new[] { "EmployeeId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_UserLoginIdentifiers_IdentifierType_NormalizedValue",
                schema: "iam",
                table: "UserLoginIdentifiers",
                columns: new[] { "IdentifierType", "NormalizedValue" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_UserLoginIdentifiers_UserId_IsActive",
                schema: "iam",
                table: "UserLoginIdentifiers",
                columns: new[] { "UserId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountActivationChallenges",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "CommunicationDeliveryAttempts",
                schema: "comms");

            migrationBuilder.DropTable(
                name: "UserLoginIdentifiers",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "CommunicationOutboxMessages",
                schema: "comms");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_FamilyId_RevokedAt",
                schema: "iam",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "IssuedAt",
                schema: "iam",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "RevocationReason",
                schema: "iam",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                schema: "iam",
                table: "RefreshTokens");
        }
    }
}
