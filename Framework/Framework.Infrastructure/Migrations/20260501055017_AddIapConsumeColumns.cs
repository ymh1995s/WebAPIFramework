using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Framework.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIapConsumeColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConsumeAttempts",
                table: "IapPurchases",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConsumedAt",
                table: "IapPurchases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastConsumeAttemptAt",
                table: "IapPurchases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastConsumeError",
                table: "IapPurchases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProductType",
                table: "IapPurchases",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsumeAttempts",
                table: "IapPurchases");

            migrationBuilder.DropColumn(
                name: "ConsumedAt",
                table: "IapPurchases");

            migrationBuilder.DropColumn(
                name: "LastConsumeAttemptAt",
                table: "IapPurchases");

            migrationBuilder.DropColumn(
                name: "LastConsumeError",
                table: "IapPurchases");

            migrationBuilder.DropColumn(
                name: "ProductType",
                table: "IapPurchases");
        }
    }
}
