using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Framework.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyRewardSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 구 DailyRewardConfigs 테이블 제거 — Current/Next 2슬롯 방식(DailyRewardSlots)으로 대체
            migrationBuilder.DropIndex(
                name: "IX_DailyRewardConfigs_ItemId",
                table: "DailyRewardConfigs");

            migrationBuilder.DropTable(
                name: "DailyRewardConfigs");

            // Players 테이블에 누적 출석 횟수 컬럼 추가 (cycleDay = AttendanceCount % 28 + 1 계산용)
            migrationBuilder.AddColumn<int>(
                name: "AttendanceCount",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // DailyLoginLogs 테이블에 지급된 보상 사이클 일자 컬럼 추가 (이력 추적용)
            migrationBuilder.AddColumn<int>(
                name: "RewardDay",
                table: "DailyLoginLogs",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // DailyRewardSlots 테이블 생성 — (Slot, Day) 복합 PK, Current/Next 2슬롯 × Day 1~28
            migrationBuilder.CreateTable(
                name: "DailyRewardSlots",
                columns: table => new
                {
                    Slot = table.Column<string>(type: "text", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<int>(type: "integer", nullable: true),
                    ItemCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyRewardSlots", x => new { x.Slot, x.Day });
                    table.ForeignKey(
                        name: "FK_DailyRewardSlots_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // DailyRewardSlots.ItemId 인덱스
            migrationBuilder.CreateIndex(
                name: "IX_DailyRewardSlots_ItemId",
                table: "DailyRewardSlots",
                column: "ItemId");

            // SystemConfig 기본값 삽입 — 이미 존재하는 경우 무시
            migrationBuilder.Sql(
                "INSERT INTO \"SystemConfigs\" (\"Key\", \"Value\") VALUES " +
                "('daily_reward_active_month', '202604'), " +
                "('daily_reward_day_boundary_hour_kst', '0'), " +
                "('daily_reward_day_boundary_minute_kst', '0') " +
                "ON CONFLICT (\"Key\") DO NOTHING;");

            // DailyRewardSlots 초기 56행 삽입 (Current/Next × 1~28, 전체 보상 없음 상태)
            var seedDate = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc);
            for (var day = 1; day <= 28; day++)
            {
                migrationBuilder.InsertData(
                    table: "DailyRewardSlots",
                    columns: new[] { "Slot", "Day", "ItemCount", "UpdatedAt" },
                    values: new object[] { "Current", day, 0, seedDate });

                migrationBuilder.InsertData(
                    table: "DailyRewardSlots",
                    columns: new[] { "Slot", "Day", "ItemCount", "UpdatedAt" },
                    values: new object[] { "Next", day, 0, seedDate });
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // IF EXISTS 사용 — 빈 Up()으로 등록된 경우 롤백 시 오류 방지
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_DailyRewardSlots_ItemId\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"DailyRewardSlots\";");

            // DailyLoginLogs.RewardDay 컬럼 제거 (없을 수도 있으므로 IF EXISTS)
            migrationBuilder.Sql(
                "ALTER TABLE \"DailyLoginLogs\" DROP COLUMN IF EXISTS \"RewardDay\";");

            // Players.AttendanceCount 컬럼 제거 (없을 수도 있으므로 IF EXISTS)
            migrationBuilder.Sql(
                "ALTER TABLE \"Players\" DROP COLUMN IF EXISTS \"AttendanceCount\";");

            // SystemConfig 시드 삭제
            migrationBuilder.Sql(
                "DELETE FROM \"SystemConfigs\" WHERE \"Key\" IN " +
                "('daily_reward_active_month', 'daily_reward_day_boundary_hour_kst', 'daily_reward_day_boundary_minute_kst');");

            // 구 DailyRewardConfigs 테이블 복원 (IF NOT EXISTS — 이미 존재하는 경우 무시)
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""DailyRewardConfigs"" (
                    ""Id"" integer GENERATED BY DEFAULT AS IDENTITY NOT NULL,
                    ""Day"" integer NOT NULL,
                    ""ItemId"" integer NOT NULL,
                    ""ItemCount"" integer NOT NULL,
                    CONSTRAINT ""PK_DailyRewardConfigs"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_DailyRewardConfigs_Items_ItemId"" FOREIGN KEY (""ItemId"")
                        REFERENCES ""Items"" (""Id"") ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS ""IX_DailyRewardConfigs_ItemId""
                    ON ""DailyRewardConfigs"" (""ItemId"");");
        }
    }
}
