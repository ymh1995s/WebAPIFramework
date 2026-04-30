using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Framework.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ContentStage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StageClears",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    StageId = table.Column<int>(type: "integer", nullable: false),
                    FirstClearedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastClearedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClearCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    BestScore = table.Column<int>(type: "integer", nullable: false),
                    BestStars = table.Column<int>(type: "integer", nullable: false),
                    BestClearTimeMs = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StageClears", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    RewardTableCode = table.Column<string>(type: "text", nullable: true),
                    RePlayRewardTableCode = table.Column<string>(type: "text", nullable: true),
                    RePlayRewardDecayPercent = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ExpReward = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    RequiredPrevStageId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stages_Stages_RequiredPrevStageId",
                        column: x => x.RequiredPrevStageId,
                        principalTable: "Stages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StageClears_PlayerId",
                table: "StageClears",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_StageClears_PlayerId_StageId",
                table: "StageClears",
                columns: new[] { "PlayerId", "StageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stages_Code",
                table: "Stages",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stages_RequiredPrevStageId",
                table: "Stages",
                column: "RequiredPrevStageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StageClears");

            migrationBuilder.DropTable(
                name: "Stages");
        }
    }
}
