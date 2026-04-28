using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Framework.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Players_DeviceId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Players_GoogleId",
                table: "Players");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Players",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Players",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MergedIntoPlayerId",
                table: "Players",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Players_DeviceId",
                table: "Players",
                column: "DeviceId",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Players_GoogleId",
                table: "Players",
                column: "GoogleId",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Players_MergedIntoPlayerId",
                table: "Players",
                column: "MergedIntoPlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Players_Players_MergedIntoPlayerId",
                table: "Players",
                column: "MergedIntoPlayerId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Players_Players_MergedIntoPlayerId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Players_DeviceId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Players_GoogleId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Players_MergedIntoPlayerId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "MergedIntoPlayerId",
                table: "Players");

            migrationBuilder.CreateIndex(
                name: "IX_Players_DeviceId",
                table: "Players",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Players_GoogleId",
                table: "Players",
                column: "GoogleId",
                unique: true);
        }
    }
}
