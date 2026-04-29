using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Framework.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRateLimitLogPlayerIdUserAgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PlayerId",
                table: "RateLimitLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "RateLimitLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActorId",
                table: "AuditLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActorType",
                table: "AuditLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_RateLimitLogs_PlayerId",
                table: "RateLimitLogs",
                column: "PlayerId",
                filter: "\"PlayerId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorType",
                table: "AuditLogs",
                column: "ActorType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RateLimitLogs_PlayerId",
                table: "RateLimitLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_ActorType",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "PlayerId",
                table: "RateLimitLogs");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "RateLimitLogs");

            migrationBuilder.DropColumn(
                name: "ActorId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ActorType",
                table: "AuditLogs");
        }
    }
}
