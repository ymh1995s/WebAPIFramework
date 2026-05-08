using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Framework.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddItemUseRewardTableId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UseRewardTableId",
                table: "Items",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_UseRewardTableId",
                table: "Items",
                column: "UseRewardTableId");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_RewardTables_UseRewardTableId",
                table: "Items",
                column: "UseRewardTableId",
                principalTable: "RewardTables",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Items_RewardTables_UseRewardTableId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_UseRewardTableId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "UseRewardTableId",
                table: "Items");
        }
    }
}
