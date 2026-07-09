using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FCG.Users.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntegrationOutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationOutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationOutboxMessages_NextAttemptAtUtc",
                table: "IntegrationOutboxMessages",
                column: "NextAttemptAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationOutboxMessages_ProcessedAtUtc",
                table: "IntegrationOutboxMessages",
                column: "ProcessedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntegrationOutboxMessages");
        }
    }
}
