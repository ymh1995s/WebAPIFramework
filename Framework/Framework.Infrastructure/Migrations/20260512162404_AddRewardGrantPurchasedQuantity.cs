using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Framework.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRewardGrantPurchasedQuantity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxPerCall",
                table: "ShopProducts",
                type: "integer",
                nullable: true);

            // PurchasedQuantity: 기존 행은 1회 구매 = 수량 1로 backfill
            migrationBuilder.AddColumn<int>(
                name: "PurchasedQuantity",
                table: "RewardGrants",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxPerCall",
                table: "ShopProducts");

            migrationBuilder.DropColumn(
                name: "PurchasedQuantity",
                table: "RewardGrants");
        }
    }
}
