using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Framework.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShopProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShopProducts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    PriceItemId = table.Column<int>(type: "integer", nullable: false),
                    PriceAmount = table.Column<int>(type: "integer", nullable: false),
                    RewardTableId = table.Column<int>(type: "integer", nullable: false),
                    DailyLimit = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TotalLimit = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShopProducts_Items_PriceItemId",
                        column: x => x.PriceItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShopProducts_RewardTables_RewardTableId",
                        column: x => x.RewardTableId,
                        principalTable: "RewardTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShopProducts_PriceItemId",
                table: "ShopProducts",
                column: "PriceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopProducts_RewardTableId",
                table: "ShopProducts",
                column: "RewardTableId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopProducts_SortOrder",
                table: "ShopProducts",
                column: "SortOrder");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShopProducts");
        }
    }
}
