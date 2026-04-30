using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Framework.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LevelThresholds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LevelThresholds",
                columns: table => new
                {
                    Level = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequiredExp = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LevelThresholds", x => x.Level);
                });

            migrationBuilder.InsertData(
                table: "LevelThresholds",
                columns: new[] { "Level", "RequiredExp", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, 0, new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, 100, new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, 250, new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, 450, new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 5, 700, new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 6, 1000, new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 7, 1400, new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 8, 1900, new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 9, 2500, new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 10, 3200, new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 11, 4000, new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 12, 5000, new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 13, 6200, new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 14, 7600, new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 15, 9200, new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 16, 11000, new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 17, 13000, new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 18, 15500, new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 19, 18500, new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20, 22000, new DateTime(2026, 4, 30, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LevelThresholds");
        }
    }
}
