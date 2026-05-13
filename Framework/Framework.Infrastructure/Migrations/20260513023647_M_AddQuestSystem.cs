using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Framework.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M_AddQuestSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuestDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Period = table.Column<short>(type: "smallint", nullable: false),
                    ConditionType = table.Column<short>(type: "smallint", nullable: false),
                    ConditionTargetId = table.Column<int>(type: "integer", nullable: true),
                    TargetAmount = table.Column<int>(type: "integer", nullable: false),
                    RewardTableId = table.Column<int>(type: "integer", nullable: false),
                    PrerequisiteQuestId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestDefinitions_QuestDefinitions_PrerequisiteQuestId",
                        column: x => x.PrerequisiteQuestId,
                        principalTable: "QuestDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_QuestDefinitions_RewardTables_RewardTableId",
                        column: x => x.RewardTableId,
                        principalTable: "RewardTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlayerQuestProgresses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    QuestId = table.Column<int>(type: "integer", nullable: false),
                    PeriodKey = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CurrentAmount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsClaimed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ClaimedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResetAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerQuestProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerQuestProgresses_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerQuestProgresses_QuestDefinitions_QuestId",
                        column: x => x.QuestId,
                        principalTable: "QuestDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerQuestProgresses_Player_PeriodKey",
                table: "PlayerQuestProgresses",
                columns: new[] { "PlayerId", "PeriodKey" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerQuestProgresses_QuestId",
                table: "PlayerQuestProgresses",
                column: "QuestId");

            migrationBuilder.CreateIndex(
                name: "UX_PlayerQuestProgresses_Player_Quest_PeriodKey",
                table: "PlayerQuestProgresses",
                columns: new[] { "PlayerId", "QuestId", "PeriodKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestDefinitions_ConditionType_ConditionTargetId",
                table: "QuestDefinitions",
                columns: new[] { "ConditionType", "ConditionTargetId" },
                filter: "\"IsActive\" = true AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_QuestDefinitions_Period_IsActive_IsDeleted",
                table: "QuestDefinitions",
                columns: new[] { "Period", "IsActive", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_QuestDefinitions_PrerequisiteQuestId",
                table: "QuestDefinitions",
                column: "PrerequisiteQuestId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestDefinitions_RewardTableId",
                table: "QuestDefinitions",
                column: "RewardTableId");

            migrationBuilder.CreateIndex(
                name: "UX_QuestDefinitions_Code",
                table: "QuestDefinitions",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerQuestProgresses");

            migrationBuilder.DropTable(
                name: "QuestDefinitions");
        }
    }
}
